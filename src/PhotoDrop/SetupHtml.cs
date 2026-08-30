namespace PhotoDrop;

/// <summary>
/// The setup screen, opened in the default browser from the tray. This replaces what used
/// to be a native window - rendering it here is why the app needs no UI framework.
/// Only ever served to 127.0.0.1.
/// </summary>
static class SetupHtml
{
    // __STATE__ is replaced with a JSON blob at request time.
    public static readonly string Page = Template
        .Replace("__THEME__", Theme.Css)
        .Replace("__SPRITE__", Theme.Sprite);

    const string Template = """
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>PhotoDrop setup</title>
<link rel="icon" href="/favicon.ico" sizes="any">
<style>
__THEME__

@layer page {
  :root {
    --qr: 240px;
  }

  body {
    /* One short column, so centre it. `safe` keeps the top reachable when the page
       outgrows the viewport - plain centring would push it out of scroll range. */
    display: grid;
    align-content: safe center;
    min-height: 100dvh;
    padding: var(--step-6) var(--step-5);
  }

  .sub {
    margin-bottom: var(--step-5);
  }

  ol {
    margin: 0 0 var(--step-5);
    padding-left: var(--step-5);

    & li {
      margin-bottom: var(--step-1);
    }
  }

  /* A card - and the only one. It sits on the page, holds nothing but the code. */
  .qr {
    display: grid;
    place-items: center;
    padding: var(--step-4);
    margin-bottom: var(--step-4);
    border: var(--hairline);
    border-radius: var(--radius-lg);
    background: var(--paper);

    & img {
      display: block;
      inline-size: var(--qr);
      block-size: var(--qr);
      image-rendering: pixelated;
    }
  }

  .addr {
    display: flex;
    align-items: center;
    gap: var(--step-2);

    & select {
      font-family: var(--font-mono);
      font-weight: 600;
    }
  }

  .field {
    flex: 1;

    &[hidden] {
      display: none;
    }

    /* appearance:none takes the native arrow with it, so put one back */
    &:has(select) {
      grid-template-columns: auto 1fr auto;
    }

    /* a single address is not a choice - drop the arrow and don't open a menu */
    &:has(option:only-child) {
      grid-template-columns: auto 1fr;

      & select {
        pointer-events: none;
      }

      & .chevron {
        display: none;
      }
    }

    & .chevron {
      pointer-events: none;
    }
  }

  .hint {
    margin-block: var(--step-2) var(--step-5);
  }

  header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: var(--step-3);
    margin-bottom: var(--step-1);

    /* the gap below the heading belongs to the row, or it skews the centring */
    & h1 {
      margin: 0;
    }
  }

  /* Icon only, so it needs a tap target the icon itself doesn't provide. The negative
     margin pulls that padding back off the column edge so the gear stays aligned. */
  .cog {
    --pad: calc((var(--tap) - var(--icon)) / 2);

    display: grid;
    place-items: center;
    inline-size: var(--tap);
    block-size: var(--tap);
    margin-right: calc(var(--pad) * -1);
    border-radius: var(--radius-pill);
    color: var(--muted);
    transition: color var(--quick);

    &:hover {
      color: var(--ink);
    }
  }

  details {

    & summary {
      display: grid;
      grid-template-columns: auto 1fr auto;
      align-items: center;
      gap: var(--step-3);
      padding-block: var(--step-3);
      border-top: var(--hairline);
      list-style: none;
      cursor: pointer;
      color: var(--accent);
      font-size: var(--size-sm);
      font-weight: 600;

      &::-webkit-details-marker {
        display: none;
      }
    }

    /* the result line keeps its own ok/bad colour */
    & p:not(#fwResult) {
      margin-top: var(--step-3);
      color: var(--muted);
      font-size: var(--size-sm);
    }

    & #fwResult {
      margin-top: var(--step-3);
      font-size: var(--size-sm);
    }

    & .btn {
      margin-top: var(--step-4);
    }

    &[open] .chevron {
      rotate: 180deg;
    }
  }

  .chevron {
    transition: rotate var(--quick);
  }

  /* No border: the dialog is its own layer above the scrim, so everything inside it
     stays borderless and is separated by rules instead. */
  dialog {
    width: min(100% - var(--step-6), var(--column));
    padding: var(--step-5);
    border: 0;
    border-radius: var(--radius-lg);
    background: var(--surface);
    color: var(--ink);
    box-shadow: var(--raise);

    /* it only takes focus so that no field does; it is not an interactive control */
    &:focus {
      outline: none;
    }

    &::backdrop {
      background: var(--scrim);
    }

    & h2 {
      margin: 0 0 var(--step-3);
      font-size: var(--size-body);
    }
  }

  .pref {
    display: grid;
    grid-template-columns: auto 1fr;
    align-items: center;
    gap: var(--step-3);
    padding-block: var(--step-3);
    border-top: var(--hairline);

    & label {
      color: var(--muted);
      font-size: var(--size-sm);
    }

    & input:not(.switch) {
      width: 100%;
      padding: 0;
      border: 0;
      background: none;
      color: var(--ink);
      font: inherit;
      text-align: right;

      /* an outline is not a border, so focus can show without nesting a box */
      &:focus-visible {
        outline: 2px solid var(--accent);
        outline-offset: var(--step-1);
        border-radius: var(--step-1);
      }
    }

    /* the picker is the way in; the path beside it is for anyone who'd rather type */
    & .folderRow {
      display: flex;
      align-items: center;
      gap: var(--step-3);

      & input {
        flex: 1;
        min-width: 0;
      }

      & .link {
        flex: none;
        padding-block: 0;
        white-space: nowrap;
      }
    }

    /* a path is too long to sit beside its label */
    &.stack {
      grid-template-columns: 1fr;
      gap: var(--step-1);

      & input {
        text-align: left;
        font-family: var(--font-mono);
        font-size: var(--size-sm);
      }
    }
  }

  #prefMsg {
    margin-top: var(--step-3);
    font-size: var(--size-sm);

    &:empty {
      display: none;
    }
  }

  .prefActions {
    display: flex;
    justify-content: flex-end;
    align-items: center;
    gap: var(--step-2);
    margin-top: var(--step-4);

    & .btn {
      width: auto;
    }
  }

  #fwResult:empty {
    display: none;
  }
}
</style>
</head>
<body>
__SPRITE__
<main>
  <header>
    <h1>Send photos from your phone</h1>
    <button type="button" id="openPrefs" class="cog" aria-label="Preferences" title="Preferences">
      <svg class="icon"><use href="#i-cog"></use></svg>
    </button>
  </header>
  <p class="sub">PhotoDrop keeps running in the tray.</p>

  <ol>
    <li>Put your phone on this Wi-Fi.</li>
    <li>Point its camera at the code.</li>
  </ol>

  <div class="qr"><img id="qr" alt="QR code for the PhotoDrop address"></div>

  <div class="addr">
    <label class="field">
      <svg class="icon"><use href="#i-phone"></use></svg>
      <select id="pick"></select>
      <svg class="icon chevron"><use href="#i-chevron"></use></svg>
    </label>
    <button id="copy" class="link">
      <svg class="icon"><use id="copyIcon" href="#i-copy"></use></svg><span id="copyText">Copy</span>
    </button>
  </div>

  <p class="hint">No camera? Type it into your phone's browser.</p>

  <details>
    <summary>
      <svg class="icon"><use href="#i-shield"></use></svg>
      <span>Phone can't connect?</span>
      <svg class="icon chevron"><use href="#i-chevron"></use></svg>
    </summary>
    <p>Check the phone is on this Wi-Fi, not mobile data, and that you typed the whole
       address including the port.</p>
    <p>If that looks right, Windows Firewall is blocking PhotoDrop.</p>
    <button id="fw" class="btn">Allow through the firewall</button>
    <p id="fwResult"></p>
  </details>

  <dialog id="prefs" tabindex="-1">
    <h2>Preferences</h2>

    <div class="pref stack">
      <label for="cfgFolder">Save photos to</label>
      <div class="folderRow">
        <input id="cfgFolder" spellcheck="false">
        <button type="button" id="cfgBrowse" class="link">
          <svg class="icon"><use href="#i-folder"></use></svg>Choose folder...
        </button>
      </div>
    </div>

    <div class="pref">
      <label for="cfgPort">Port</label>
      <input id="cfgPort" type="number" min="1" max="65535" inputmode="numeric">
    </div>

    <div class="pref">
      <label for="cfgPin">PIN</label>
      <input id="cfgPin" placeholder="none" autocomplete="off">
    </div>

    <div class="pref">
      <label for="cfgDate">Organize photos into folders by date</label>
      <input type="checkbox" id="cfgDate" class="switch">
    </div>

    <p id="prefMsg"></p>

    <div class="prefActions">
      <button type="button" id="prefCancel" class="link">Cancel</button>
      <button type="button" id="prefApply" class="btn">Apply</button>
    </div>
  </dialog>
</main>

<script>
const STATE = __STATE__;
const qr = document.getElementById('qr');
const pick = document.getElementById('pick');
let address = '';

STATE.addresses.forEach((a) => {
  const option = document.createElement('option');
  option.value = a;
  option.textContent = `http://${a}:${STATE.port}`;
  pick.appendChild(option);
});

function render() {
  address = `http://${pick.value}:${STATE.port}`;
  qr.src = '/setup-qr.png?u=' + encodeURIComponent(address);
}
pick.addEventListener('change', render);
render();

const copyIcon = document.getElementById('copyIcon');
const copyText = document.getElementById('copyText');

document.getElementById('copy').addEventListener('click', async () => {
  try {
    await navigator.clipboard.writeText(address);
  } catch {
    // the clipboard API needs a secure context in some browsers
    const scratch = document.createElement('textarea');
    scratch.value = address;
    document.body.append(scratch);
    scratch.select();
    document.execCommand('copy');
    scratch.remove();
  }
  copyIcon.setAttribute('href', '#i-check');
  copyText.textContent = 'Copied';
  setTimeout(() => {
    copyIcon.setAttribute('href', '#i-copy');
    copyText.textContent = 'Copy';
  }, 1400);
});

const prefs = document.getElementById('prefs');
const cfgFolder = document.getElementById('cfgFolder');
const cfgPort = document.getElementById('cfgPort');
const cfgPin = document.getElementById('cfgPin');
const cfgDate = document.getElementById('cfgDate');
const prefMsg = document.getElementById('prefMsg');
const prefApply = document.getElementById('prefApply');

document.getElementById('openPrefs').addEventListener('click', () => {
  setFolder(STATE.folder);
  cfgPort.value = STATE.port;
  cfgPin.value = STATE.pin;
  cfgDate.checked = STATE.organizeByDate;
  prefMsg.textContent = '';
  prefMsg.className = '';
  prefs.showModal();
  prefs.focus();   // opening a settings panel shouldn't drop a caret in the first field
  cfgFolder.scrollLeft = 0;   // a hidden field has no box to scroll, so do it once shown
});

document.getElementById('prefCancel').addEventListener('click', () => prefs.close());

// A path longer than the box would otherwise sit scrolled to its tail, hiding the drive.
function setFolder(path) {
  cfgFolder.value = path;
  cfgFolder.scrollLeft = 0;
}

const cfgBrowse = document.getElementById('cfgBrowse');
cfgBrowse.addEventListener('click', async () => {
  cfgBrowse.disabled = true;
  try {
    // Resolves only once the native dialog is closed.
    const res = await fetch('/api/pick-folder', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ current: cfgFolder.value }),
    });
    const data = await res.json();
    if (data.ok) setFolder(data.folder);
  } catch {
    /* cancelled or unavailable - leave what's typed alone */
  }
  cfgBrowse.disabled = false;
});

prefApply.addEventListener('click', async () => {
  prefApply.disabled = true;
  prefMsg.className = '';
  prefMsg.textContent = 'Applying...';
  try {
    const res = await fetch('/api/config', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        folder: cfgFolder.value,
        port: Number(cfgPort.value),
        pin: cfgPin.value,
        organizeByDate: cfgDate.checked,
      }),
    });
    const data = await res.json();

    if (!data.ok) {
      prefMsg.textContent = data.error;
      prefMsg.className = 'bad';
    } else if (data.moved) {
      // This page is talking to the listener that is about to stop.
      prefMsg.textContent = 'Moved to port ' + data.port + '.';
      prefMsg.className = 'ok';
      setTimeout(() => { location.href = 'http://127.0.0.1:' + data.port + '/setup'; }, 1200);
      return;
    } else {
      STATE.folder = data.folder;
      STATE.pin = cfgPin.value.trim();
      STATE.organizeByDate = cfgDate.checked;
      prefs.close();
    }
  } catch {
    prefMsg.textContent = 'Something went wrong.';
    prefMsg.className = 'bad';
  }
  prefApply.disabled = false;
});

document.getElementById('fw').addEventListener('click', async (e) => {
  const out = document.getElementById('fwResult');
  e.target.disabled = true;
  out.textContent = 'Waiting for the Windows prompt...';
  out.className = '';
  try {
    const res = await fetch('/api/firewall', { method: 'POST' });
    const data = await res.json();
    out.textContent = data.ok
      ? 'Done. Try the address on your phone again.'
      : "Blocked - permission was declined, or this account isn't an administrator.";
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
