// GitHub Releases REST API のレスポンスを定期取得して KV に保存し、
// そのまま (加工せず) 配信する Worker。

const KEY_BODY = "releases:body";
const KEY_META = "releases:meta"; // { etag, updatedAt, checkedAt, status }

/** GitHub API を呼び、KV を更新する */
async function refresh(env) {
    const repo = env.GITHUB_REPO;
    const perPage = Math.min(Number(env.PER_PAGE) || 30, 100);
    const url = `https://api.github.com/repos/${repo}/releases?per_page=${perPage}`;

    const meta = (await env.RELEASES_KV.get(KEY_META, "json")) || {};

    const headers = {
        Accept: "application/vnd.github+json",
        "X-GitHub-Api-Version": "2022-11-28",
        "User-Agent": "cloudflare-worker-github-releases-mirror",
    };
    if (env.GITHUB_TOKEN) headers.Authorization = `Bearer ${env.GITHUB_TOKEN}`;
    // 条件付きリクエスト: 304 はレート制限を消費しない
    if (meta.etag) headers["If-None-Match"] = meta.etag;

    const res = await fetch(url, { headers });
    const now = new Date().toISOString();

    if (res.status === 304) {
        await env.RELEASES_KV.put(
            KEY_META,
            JSON.stringify({ ...meta, checkedAt: now, status: 304 })
        );
        return { updated: false, status: 304 };
    }

    if (!res.ok) {
        // 失敗時は既存データを保持し、状態だけ記録
        await env.RELEASES_KV.put(
            KEY_META,
            JSON.stringify({ ...meta, checkedAt: now, status: res.status })
        );
        console.error(`GitHub API error: ${res.status} ${await res.text()}`);
        return { updated: false, status: res.status };
    }

    const body = await res.text(); // 加工せずそのまま保存
    await env.RELEASES_KV.put(KEY_BODY, body);
    await env.RELEASES_KV.put(
        KEY_META,
        JSON.stringify({
            etag: res.headers.get("etag"),
            updatedAt: now,
            checkedAt: now,
            status: 200,
            repo,
        })
    );
    return { updated: true, status: 200 };
}

function escapeHtml(s) {
    return s
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;");
}

function prettyJson(raw) {
    try {
        return JSON.stringify(JSON.parse(raw), null, 2);
    } catch {
        return raw;
    }
}

async function load(env, ctx) {
    let body = await env.RELEASES_KV.get(KEY_BODY);
    // 初回デプロイ直後など、cron 実行前でデータが無い場合のみその場で取得
    if (body === null) {
        await refresh(env);
        body = await env.RELEASES_KV.get(KEY_BODY);
    }
    const meta = (await env.RELEASES_KV.get(KEY_META, "json")) || {};
    return { body, meta };
}

export default {
    async scheduled(event, env, ctx) {
        ctx.waitUntil(refresh(env));
    },

    async fetch(request, env, ctx) {
        const { pathname } = new URL(request.url);

        if (pathname === "/releases.json") {
            const { body } = await load(env, ctx);
            if (body === null) {
                return new Response('{"error":"data not available yet"}', {
                    status: 503,
                    headers: { "Content-Type": "application/json; charset=utf-8" },
                });
            }
            return new Response(body, {
                headers: {
                    "Content-Type": "application/json; charset=utf-8",
                    "Cache-Control": "public, max-age=300",
                    "Access-Control-Allow-Origin": "*",
                },
            });
        }

        if (pathname === "/") {
            const { body, meta } = await load(env, ctx);
            const repo = env.GITHUB_REPO;
            const content =
                body === null
                    ? "データをまだ取得できていません。しばらくしてから再読み込みしてください。"
                    : escapeHtml(prettyJson(body));

            const html = `<!doctype html>
<html lang="ja">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>${escapeHtml(repo)} Releases (GitHub API mirror)</title>
<style>
  body{font-family:system-ui,sans-serif;margin:0;padding:1rem;line-height:1.5}
  header{margin-bottom:1rem}
  pre{background:#f6f8fa;color:#1f2328;padding:1rem;overflow:auto;border-radius:6px;font-size:.85rem}
  @media (prefers-color-scheme: dark){
    body{background:#0d1117;color:#e6edf3}
    pre{background:#161b22;color:#e6edf3}
    a{color:#58a6ff}
  }
</style>
</head>
<body>
<header>
  <h1>${escapeHtml(repo)} Releases</h1>
  <p>GitHub REST API <code>GET /repos/${escapeHtml(repo)}/releases</code> のレスポンスをそのまま転載しています。</p>
  <p>最終更新: ${escapeHtml(meta.updatedAt || "-")} / 最終確認: ${escapeHtml(meta.checkedAt || "-")} (status ${escapeHtml(String(meta.status ?? "-"))})
  ・ <a href="/releases.json">raw JSON</a></p>
</header>
<pre>${content}</pre>
</body>
</html>`;

            return new Response(html, {
                headers: {
                    "Content-Type": "text/html; charset=utf-8",
                    "Cache-Control": "public, max-age=300",
                },
            });
        }

        return new Response("Not Found", { status: 404 });
    },
};