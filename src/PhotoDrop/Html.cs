static class Html
{
    // __PIN_REQUIRED__ and __FOLDER__ are substituted at startup from config.json.
    public const string Page = """
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover">
<meta name="theme-color" content="#111318">
<title>PhotoDrop</title>
<link rel="icon" href="/favicon.ico" sizes="any">
<link rel="icon" type="image/png" sizes="192x192" href="/icon-192.png">
<link rel="apple-touch-icon" href="/apple-touch-icon.png">
<link rel="manifest" href="/manifest.webmanifest">
<meta name="apple-mobile-web-app-capable" content="yes">
<meta name="mobile-web-app-capable" content="yes">
<meta name="apple-mobile-web-app-title" content="PhotoDrop">
<meta name="apple-mobile-web-app-status-bar-style" content="black-translucent">
<style>
  :root {
    --bg: #f6f7f9; --card: #ffffff; --ink: #14161a; --muted: #6b7280;
    --line: #e4e7ec; --accent: #2f6df6; --ok: #15803d; --bad: #b91c1c;
  }
  @media (prefers-color-scheme: dark) {
    :root {
      --bg: #101216; --card: #191c22; --ink: #e9ecf1; --muted: #9aa3b2;
      --line: #2a2f39; --accent: #4d86ff; --ok: #4ade80; --bad: #f87171;
    }
  }
  * { box-sizing: border-box; }
  body {
    margin: 0; background: var(--bg); color: var(--ink);
    font: 16px/1.5 -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
    padding: max(20px, env(safe-area-inset-top)) 20px calc(28px + env(safe-area-inset-bottom));
    display: flex; justify-content: center;
  }
  main { width: 100%; max-width: 460px; }
  h1 { font-size: 22px; margin: 4px 0 2px; letter-spacing: -0.01em; }
  .sub { color: var(--muted); font-size: 14px; margin: 0 0 22px; word-break: break-all; }
  button {
    -webkit-appearance: none; appearance: none; font: inherit; font-weight: 600;
    width: 100%; padding: 20px; border: 0; border-radius: 16px;
    background: var(--accent); color: #fff; cursor: pointer;
    box-shadow: 0 6px 18px rgba(47, 109, 246, .28);
  }
  button:active { transform: translateY(1px); }
  button:disabled { opacity: .55; box-shadow: none; cursor: default; }
  #pinRow { display: none; margin-bottom: 12px; }
  #pinRow input {
    font: inherit; width: 100%; padding: 14px; border-radius: 12px;
    border: 1px solid var(--line); background: var(--card); color: var(--ink);
  }
  #status { margin: 18px 0 8px; font-size: 14px; color: var(--muted); min-height: 20px; }
  ul { list-style: none; margin: 0; padding: 0; display: grid; gap: 8px; }
  li {
    background: var(--card); border: 1px solid var(--line); border-radius: 12px;
    padding: 11px 13px; font-size: 14px;
  }
  .row { display: flex; gap: 10px; align-items: baseline; }
  .name { flex: 1; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
  .pct { color: var(--muted); font-variant-numeric: tabular-nums; font-size: 13px; }
  .bar { height: 3px; border-radius: 2px; background: var(--line); margin-top: 8px; overflow: hidden; }
  .bar > i { display: block; height: 100%; width: 0; background: var(--accent); transition: width .15s; }
  li.done .pct { color: var(--ok); }
  li.done .bar { display: none; }
  li.fail .pct { color: var(--bad); }
  li.fail .bar { display: none; }
  footer { margin-top: 26px; font-size: 12px; color: var(--muted); text-align: center; }
</style>
</head>
<body>
<main>
  <h1>PhotoDrop</h1>
  <p class="sub">Files land in <b>__FOLDER__</b> on your PC.</p>

  <div id="pinRow"><input id="pin" type="password" inputmode="numeric" placeholder="PIN" autocomplete="off"></div>

  <button id="go">Transfer photos</button>
  <input id="picker" type="file" multiple accept="image/*,video/*" hidden>

  <div id="status"></div>
  <ul id="list"></ul>

  <noscript>
    <form action="/upload-form" method="post" enctype="multipart/form-data">
      <p style="color:var(--muted);font-size:14px">JavaScript is off — use this instead:</p>
      <!-- pin must come before the file: the server reads sections in order -->
      <input type="text" name="pin" placeholder="PIN (if set)">
      <input type="file" name="files" multiple>
      <button type="submit" style="margin-top:12px">Upload</button>
    </form>
  </noscript>

  <footer>Local network only — nothing leaves your Wi-Fi.</footer>
</main>

<script>
const PIN_REQUIRED = __PIN_REQUIRED__;
const go = document.getElementById('go');
const picker = document.getElementById('picker');
const list = document.getElementById('list');
const status = document.getElementById('status');
const pinRow = document.getElementById('pinRow');
const pinInput = document.getElementById('pin');

if (PIN_REQUIRED) {
  pinRow.style.display = 'block';
  pinInput.value = localStorage.getItem('photodrop.pin') || '';
}

go.addEventListener('click', () => picker.click());
picker.addEventListener('change', () => {
  const files = [...picker.files];
  picker.value = '';                 // let the same photo be picked again later
  if (files.length) send(files);
});

function addRow(file) {
  const li = document.createElement('li');
  li.innerHTML =
    '<div class="row"><span class="name"></span><span class="pct">0%</span></div>' +
    '<div class="bar"><i></i></div>';
  li.querySelector('.name').textContent = file.name;
  list.prepend(li);
  return {
    li,
    pct: li.querySelector('.pct'),
    fill: li.querySelector('.bar > i'),
  };
}

function upload(file, row) {
  return new Promise((resolve, reject) => {
    const xhr = new XMLHttpRequest();
    xhr.open('POST', '/upload');
    xhr.setRequestHeader('Content-Type', 'application/octet-stream');
    xhr.setRequestHeader('X-File-Name', encodeURIComponent(file.name));
    if (PIN_REQUIRED) xhr.setRequestHeader('X-Pin', pinInput.value.trim());

    xhr.upload.onprogress = (e) => {
      if (!e.lengthComputable) return;
      const p = Math.round((e.loaded / e.total) * 100);
      row.pct.textContent = p + '%';
      row.fill.style.width = p + '%';
    };
    xhr.onload = () => (xhr.status === 200 ? resolve() : reject(new Error(
      xhr.status === 401 ? 'wrong PIN' : 'error ' + xhr.status)));
    xhr.onerror = () => reject(new Error('connection lost'));
    xhr.send(file);
  });
}

async function send(files) {
  go.disabled = true;
  if (PIN_REQUIRED) localStorage.setItem('photodrop.pin', pinInput.value.trim());

  let done = 0, failed = 0;
  for (const file of files) {
    const row = addRow(file);
    status.textContent = `Sending ${done + failed + 1} of ${files.length}…`;
    try {
      await upload(file, row);
      row.li.classList.add('done');
      row.pct.textContent = 'Saved';
      done++;
    } catch (err) {
      row.li.classList.add('fail');
      row.pct.textContent = err.message;
      failed++;
    }
  }

  status.textContent = failed
    ? `${done} saved, ${failed} failed.`
    : `All ${done} saved to your PC.`;
  go.disabled = false;
}
</script>
</body>
</html>
""";
}
