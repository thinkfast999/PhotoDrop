namespace PhotoDrop;

/// <summary>
/// The setup screen, opened in the default browser from the tray. This replaces what used
/// to be a native window - rendering it here is why the app needs no UI framework.
/// Only ever served to 127.0.0.1.
/// </summary>
static class SetupHtml
{
    // __STATE__ is replaced with a JSON blob at request time.
    public const string Page = """
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>PhotoDrop setup</title>
<link rel="icon" href="/favicon.ico" sizes="any">
<style>
  :root {
    --bg: #f6f7f9; --card: #fff; --ink: #14161a; --muted: #6b7280;
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
    margin: 0; min-height: 100vh; background: var(--bg); color: var(--ink);
    font: 15px/1.55 -apple-system, "Segoe UI", Roboto, sans-serif;
    display: flex; justify-content: center; padding: 32px 20px 48px;
  }
  main { width: 100%; max-width: 420px; }
  h1 { font-size: 20px; margin: 0 0 4px; letter-spacing: -0.01em; }
  .sub { color: var(--muted); margin: 0 0 24px; font-size: 14px; }
  ol { margin: 0 0 20px; padding-left: 22px; }
  li { margin-bottom: 4px; }
  .qr {
    background: #fff; border: 1px solid var(--line); border-radius: 16px;
    padding: 16px; display: flex; justify-content: center; margin-bottom: 18px;
  }
  .qr img { width: 240px; height: 240px; display: block; image-rendering: pixelated; }
  .addr { display: flex; gap: 8px; margin-bottom: 6px; }
  .addr input {
    flex: 1; font: 600 15px/1 ui-monospace, Consolas, monospace; text-align: center;
    padding: 12px; border-radius: 10px; border: 1px solid var(--line);
    background: var(--card); color: var(--ink);
  }
  button {
    font: inherit; padding: 11px 14px; border-radius: 10px; border: 1px solid var(--line);
    background: var(--card); color: var(--ink); cursor: pointer;
  }
  button:hover { border-color: var(--accent); }
  button.primary { background: var(--accent); color: #fff; border-color: var(--accent); font-weight: 600; }
  .hint { color: var(--muted); font-size: 13px; margin: 0 0 22px; }
  .row { display: flex; align-items: center; gap: 9px; padding: 12px 0; border-top: 1px solid var(--line); }
  .row label { cursor: pointer; }
  select { font: inherit; padding: 8px; border-radius: 8px; border: 1px solid var(--line);
           background: var(--card); color: var(--ink); width: 100%; }
  .folder { font-size: 13px; color: var(--muted); word-break: break-all; }
  details { border-top: 1px solid var(--line); padding-top: 12px; margin-top: 4px; }
  summary { cursor: pointer; color: var(--accent); font-size: 14px; }
  details p { font-size: 13.5px; color: var(--muted); }
  #fwResult { font-size: 13.5px; margin-top: 8px; }
  .ok { color: var(--ok); } .bad { color: var(--bad); }
</style>
</head>
<body>
<main>
  <h1>Send photos from your phone</h1>
  <p class="sub">Leave PhotoDrop running in the tray. This page is only for setting things up.</p>

  <ol>
    <li>Put your phone on the same Wi-Fi as this PC.</li>
    <li>Open the phone camera and point it at this code.</li>
  </ol>

  <div class="qr"><img id="qr" alt="QR code for the PhotoDrop address"></div>

  <div class="addr">
    <input id="url" readonly>
    <button id="copy" title="Copy address">Copy</button>
  </div>
  <p class="hint">No camera? Type that into the phone's browser.</p>

  <div id="pickRow" class="row" hidden>
    <select id="pick"></select>
  </div>

  <div class="row">
    <input type="checkbox" id="startup">
    <label for="startup">Start PhotoDrop when I sign in to Windows</label>
  </div>

  <div class="row">
    <span class="folder">Photos are saved to <b id="folder"></b><br>
    Change it from the tray icon &rarr; Choose folder...</span>
  </div>

  <details>
    <summary>Phone can't connect?</summary>
    <p>
      Check the phone is on the same Wi-Fi as this PC (not mobile data), and that you
      typed the whole address including the port at the end.
    </p>
    <p>
      If both look right, Windows Firewall is probably blocking PhotoDrop &mdash; that
      happens if the network prompt was dismissed the first time it ran.
    </p>
    <button id="fw" class="primary">Let PhotoDrop through the firewall</button>
    <div id="fwResult"></div>
  </details>
</main>

<script>
const STATE = __STATE__;
const qr = document.getElementById('qr');
const url = document.getElementById('url');
const pick = document.getElementById('pick');
const pickRow = document.getElementById('pickRow');
const startup = document.getElementById('startup');

document.getElementById('folder').textContent = STATE.folder;
startup.checked = STATE.startup;

STATE.addresses.forEach((a, i) => {
  const option = document.createElement('option');
  option.value = a;
  option.textContent = i === 0 ? `${a} (recommended)` : a;
  pick.appendChild(option);
});
if (STATE.addresses.length > 1) pickRow.hidden = false;

function render() {
  const address = `http://${pick.value}:${STATE.port}`;
  url.value = address;
  qr.src = '/setup-qr.png?u=' + encodeURIComponent(address);
}
pick.addEventListener('change', render);
render();

document.getElementById('copy').addEventListener('click', async (e) => {
  try {
    await navigator.clipboard.writeText(url.value);
  } catch {
    url.select();                       // clipboard API needs a secure context in some browsers
    document.execCommand('copy');
  }
  e.target.textContent = 'Copied';
  setTimeout(() => (e.target.textContent = 'Copy'), 1400);
});

startup.addEventListener('change', async () => {
  const wanted = startup.checked;
  try {
    const res = await fetch('/api/startup', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ enabled: wanted }),
    });
    const data = await res.json();
    startup.checked = data.enabled;     // trust the registry, not the click
  } catch {
    startup.checked = !wanted;
  }
});

document.getElementById('fw').addEventListener('click', async (e) => {
  const out = document.getElementById('fwResult');
  e.target.disabled = true;
  out.textContent = 'Waiting for the Windows permission prompt...';
  out.className = '';
  try {
    const res = await fetch('/api/firewall', { method: 'POST' });
    const data = await res.json();
    out.textContent = data.ok
      ? 'Done. Try the address on your phone again.'
      : "The rule wasn't added - permission was declined, or this account isn't an administrator.";
    out.className = data.ok ? 'ok' : 'bad';
  } catch {
    out.textContent = 'Something went wrong.';
    out.className = 'bad';
  }
  e.target.disabled = false;
});
</script>
</body>
</html>
""";
}
