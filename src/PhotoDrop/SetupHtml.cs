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
    padding: var(--step-6) var(--step-5) calc(var(--step-6) * 1.5);
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

    & input {
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

    & .chevron {
      pointer-events: none;
    }
  }

  #pickRow {
    margin-top: var(--step-2);
  }

  .hint {
    margin-block: var(--step-2) var(--step-5);
  }

  details {
    padding-top: var(--step-3);
    border-top: var(--hairline);

    & summary {
      display: grid;
      grid-template-columns: auto 1fr auto;
      align-items: center;
      gap: var(--step-3);
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

  #fwResult:empty {
    display: none;
  }
}
</style>
</head>
<body>
__SPRITE__
<main>
  <h1>Send photos from your phone</h1>
  <p class="sub">Keep PhotoDrop running in the tray.</p>

  <ol>
    <li>Put your phone on this Wi-Fi.</li>
    <li>Point its camera at the code.</li>
  </ol>

  <div class="qr"><img id="qr" alt="QR code for the PhotoDrop address"></div>

  <div class="addr">
    <span class="field">
      <svg class="icon"><use href="#i-phone"></use></svg>
      <input id="url" readonly>
    </span>
    <button id="copy" class="link">
      <svg class="icon"><use id="copyIcon" href="#i-copy"></use></svg><span id="copyText">Copy</span>
    </button>
  </div>

  <label class="field" id="pickRow" hidden>
    <svg class="icon"><use href="#i-wifi"></use></svg>
    <select id="pick"></select>
    <svg class="icon chevron"><use href="#i-chevron"></use></svg>
  </label>

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
</main>

<script>
const STATE = __STATE__;
const qr = document.getElementById('qr');
const url = document.getElementById('url');
const pick = document.getElementById('pick');
const pickRow = document.getElementById('pickRow');

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

const copyIcon = document.getElementById('copyIcon');
const copyText = document.getElementById('copyText');

document.getElementById('copy').addEventListener('click', async () => {
  try {
    await navigator.clipboard.writeText(url.value);
  } catch {
    url.select();                       // clipboard API needs a secure context in some browsers
    document.execCommand('copy');
  }
  copyIcon.setAttribute('href', '#i-check');
  copyText.textContent = 'Copied';
  setTimeout(() => {
    copyIcon.setAttribute('href', '#i-copy');
    copyText.textContent = 'Copy';
  }, 1400);
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
