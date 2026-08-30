namespace PhotoDrop;

/// <summary>The page the phone sees. One screen, one action.</summary>
static class Html
{
    // __PIN_REQUIRED__ and __FOLDER__ are substituted per request from config.json.
    public static readonly string Page = Fill(PageTemplate);

    // __MSG__ is substituted after a form post from a browser with JavaScript switched off.
    public static readonly string Receipt = Fill(ReceiptTemplate);

    static string Fill(string template) => template
        .Replace("__HEAD__", Head)
        .Replace("__THEME__", Theme.Css)
        .Replace("__SPRITE__", Theme.Sprite)
        .Replace("__BG_LIGHT__", Theme.BgLight)
        .Replace("__BG_DARK__", Theme.BgDark);

    const string Head = """
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover">
<meta name="theme-color" content="__BG_LIGHT__" media="(prefers-color-scheme: light)">
<meta name="theme-color" content="__BG_DARK__" media="(prefers-color-scheme: dark)">
<title>PhotoDrop</title>
<link rel="icon" href="/favicon.ico" sizes="any">
<link rel="icon" type="image/png" sizes="192x192" href="/icon-192.png">
<link rel="apple-touch-icon" href="/apple-touch-icon.png">
<link rel="manifest" href="/manifest.webmanifest">
<meta name="apple-mobile-web-app-capable" content="yes">
<meta name="mobile-web-app-capable" content="yes">
<meta name="apple-mobile-web-app-title" content="PhotoDrop">
<meta name="apple-mobile-web-app-status-bar-style" content="black-translucent">
""";

    const string PageTemplate = """
<!doctype html>
<html lang="en">
<head>
__HEAD__
<style>
__THEME__

@layer page {
  body {
    padding: max(var(--step-5), env(safe-area-inset-top)) var(--step-5)
             calc(var(--step-6) + env(safe-area-inset-bottom));
  }

  .sub {
    display: flex;
    align-items: center;
    gap: var(--step-2);
    margin-bottom: var(--step-5);
    overflow-wrap: anywhere;
  }

  /* Once files are moving, the folder line has done its job. */
  main:has(#list > li) .sub {
    display: none;
  }

  .field[hidden] {
    display: none;
  }

  #pin {
    margin-bottom: var(--step-3);
  }

  #status {
    margin-block: var(--step-4) var(--step-2);
    color: var(--muted);
    font-size: var(--size-sm);

    &:empty {
      display: none;
    }
  }

  ul {
    display: grid;
    gap: var(--step-2);
    margin: 0;
    padding: 0;
    list-style: none;
  }

  li {
    padding: var(--step-3);
    border: var(--hairline);
    border-radius: var(--radius-sm);
    background: var(--surface);
    font-size: var(--size-sm);

    & .row {
      display: flex;
      align-items: center;
      gap: var(--step-2);
    }

    & .name {
      flex: 1;
      overflow: hidden;
      white-space: nowrap;
      text-overflow: ellipsis;
    }

    & .pct {
      color: var(--muted);
      font-variant-numeric: tabular-nums;
      font-size: var(--size-xs);
    }

    & .mark {
      display: none;
    }

    & .bar {
      height: 3px;
      margin-top: var(--step-2);
      border-radius: var(--radius-pill);
      background: var(--line);
      overflow: hidden;

      & > i {
        display: block;
        height: 100%;
        width: 0;
        background: var(--accent);
        transition: width var(--quick);
      }
    }

    &:is(.done, .fail) {
      & .mark {
        display: block;
      }

      & .bar {
        display: none;
      }
    }

    &.done :is(.pct, .mark) {
      color: var(--ok);
    }

    &.fail :is(.pct, .mark) {
      color: var(--bad);
    }
  }

  :is(noscript, #plain) {
    display: grid;
    gap: var(--step-3);
    justify-items: start;
  }

  #plain .btn {
    justify-self: stretch;
  }

  footer {
    display: flex;
    justify-content: center;
    align-items: center;
    gap: var(--step-2);
    margin-top: var(--step-6);
    color: var(--muted);
    font-size: var(--size-xs);
  }
}
</style>
</head>
<body>
__SPRITE__
<main>
  <h1>PhotoDrop</h1>
  <p class="sub"><svg class="icon"><use href="#i-folder"></use></svg>__FOLDER__</p>

  <label class="field" id="pin" hidden>
    <svg class="icon"><use href="#i-lock"></use></svg>
    <input id="pinInput" type="password" inputmode="numeric" placeholder="PIN" autocomplete="off">
  </label>

  <button id="go" class="btn"><svg class="icon"><use href="#i-upload"></use></svg>Send photos</button>
  <input id="picker" type="file" multiple accept="image/*,video/*" hidden>

  <p id="status"></p>
  <ul id="list"></ul>

  <noscript>
    <p class="hint">JavaScript is off. Use this instead:</p>
    <!-- pin must come before the file: the server reads sections in order -->
    <label class="field"><svg class="icon"><use href="#i-lock"></use></svg>
      <input form="plain" type="text" name="pin" placeholder="PIN"></label>
    <form id="plain" action="/upload-form" method="post" enctype="multipart/form-data">
      <input type="file" name="files" multiple>
      <button type="submit" class="btn">Send</button>
    </form>
  </noscript>

  <footer><svg class="icon"><use href="#i-wifi"></use></svg>Nothing leaves your Wi-Fi.</footer>
</main>

<script>
const PIN_REQUIRED = __PIN_REQUIRED__;
const go = document.getElementById('go');
const picker = document.getElementById('picker');
const list = document.getElementById('list');
const status = document.getElementById('status');
const pin = document.getElementById('pin');
const pinInput = document.getElementById('pinInput');

if (PIN_REQUIRED) {
  pin.hidden = false;
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
    '<div class="row"><span class="name"></span><span class="pct">0%</span>' +
    '<svg class="icon mark"><use></use></svg></div>' +
    '<div class="bar"><i></i></div>';
  li.querySelector('.name').textContent = file.name;
  list.prepend(li);
  return {
    li,
    pct: li.querySelector('.pct'),
    fill: li.querySelector('.bar > i'),
    mark: li.querySelector('.mark > use'),
  };
}

function finish(row, state, label, icon) {
  row.li.classList.add(state);
  row.pct.textContent = label;
  row.mark.setAttribute('href', icon);
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
    status.textContent = `Sending ${done + failed + 1} of ${files.length}...`;
    try {
      await upload(file, row);
      finish(row, 'done', 'Saved', '#i-check');
      done++;
    } catch (err) {
      finish(row, 'fail', err.message, '#i-x');
      failed++;
    }
  }

  status.textContent = failed ? `${done} saved, ${failed} failed.` : `All ${done} saved.`;
  go.disabled = false;
}
</script>
</body>
</html>
""";

    const string ReceiptTemplate = """
<!doctype html>
<html lang="en">
<head>
__HEAD__
<style>
__THEME__

@layer page {
  body {
    display: grid;
    place-items: center;
    min-height: 100dvh;
    padding: var(--step-5);
    text-align: center;
  }

  main {
    display: grid;
    justify-items: center;
    gap: var(--step-3);
  }

  .icon {
    --icon: 32px;
    color: var(--ok);
  }
}
</style>
</head>
<body>
__SPRITE__
<main>
  <svg class="icon"><use href="#i-check"></use></svg>
  <h1>__MSG__</h1>
  <a class="link" href="/">Send more</a>
</main>
</body>
</html>
""";
}
