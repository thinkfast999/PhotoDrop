namespace PhotoDrop;

/// <summary>
/// The single source of truth for how both pages look. The phone page and the setup screen
/// share it so a colour or a radius can never drift apart between the two.
/// </summary>
static class Theme
{
    // Also needed outside CSS - the manifest and the <meta name="theme-color"> tags, neither
    // of which can read a custom property.
    public const string BgLight = "#f6f7f9";
    public const string BgDark = "#101216";
    public const string Accent = "#2f6df6";

    /// <summary>Tokens plus the handful of controls both pages build from.</summary>
    public const string Css = $$"""
@layer tokens, base, page;

@layer tokens {
  :root {
    color-scheme: light dark;

    --bg: {{BgLight}};
    --surface: #ffffff;
    --ink: #14161a;
    --muted: #6b7280;
    --line: #e4e7ec;
    --accent: {{Accent}};
    --on-accent: #ffffff;
    --ok: #15803d;
    --bad: #b91c1c;
    --paper: #ffffff;

    --step-1: 4px;
    --step-2: 8px;
    --step-3: 12px;
    --step-4: 16px;
    --step-5: 22px;
    --step-6: 32px;

    --radius-sm: 10px;
    --radius-md: 14px;
    --radius-lg: 18px;
    --radius-pill: 999px;

    --font: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
    --font-mono: ui-monospace, Consolas, monospace;
    --size-title: 21px;
    --size-body: 16px;
    --size-sm: 14px;
    --size-xs: 12px;
    --leading: 1.5;

    --icon: 18px;
    --tap: 46px;
    --column: 26rem;
    --hairline: 1px solid var(--line);
    --lift: 0 6px 18px color-mix(in srgb, var(--accent) 26%, transparent);
    --raise: 0 24px 60px rgb(0 0 0 / 0.4);
    --scrim: rgb(0 0 0 / 0.5);
    --quick: 140ms ease;
  }

  @media (prefers-color-scheme: dark) {
    :root {
      --bg: {{BgDark}};
      --surface: #191c22;
      --ink: #e9ecf1;
      --muted: #9aa3b2;
      --line: #2a2f39;
      --accent: #4d86ff;
      --ok: #4ade80;
      --bad: #f87171;
    }
  }
}

@layer base {
  * {
    box-sizing: border-box;
  }

  body {
    margin: 0;
    background: var(--bg);
    color: var(--ink);
    font: var(--size-body)/var(--leading) var(--font);
  }

  main {
    width: 100%;
    max-width: var(--column);
    margin-inline: auto;
  }

  h1 {
    margin: 0 0 var(--step-1);
    font-size: var(--size-title);
    letter-spacing: -0.01em;
  }

  p {
    margin: 0;
  }

  .sub,
  .hint {
    color: var(--muted);
    font-size: var(--size-sm);
  }

  /* The symbol library itself draws nothing. It still lays out as a 300x150 box unless
     it is taken out of flow - the `hidden` attribute does not apply to SVG. */
  .sprite {
    position: absolute;
    inline-size: 0;
    block-size: 0;
    overflow: hidden;
  }

  /* Presentation attributes stay off the sprite: stroke properties inherit, so one rule
     here styles every icon on both pages. */
  .icon {
    inline-size: var(--icon);
    block-size: var(--icon);
    flex: none;
    fill: none;
    stroke: currentColor;
    stroke-width: 2;
    stroke-linecap: round;
    stroke-linejoin: round;
  }

  /* The only bordered control. Its own children are borderless, so a field can sit
     anywhere without ever stacking outlines. */
  .field {
    display: grid;
    grid-template-columns: auto 1fr;
    align-items: center;
    gap: var(--step-2);
    min-height: var(--tap);
    padding-inline: var(--step-3);
    border: var(--hairline);
    border-radius: var(--radius-sm);
    background: var(--surface);
    color: var(--muted);
    transition: border-color var(--quick);

    &:focus-within {
      border-color: var(--accent);
    }

    & :is(input, select) {
      width: 100%;
      padding-block: var(--step-3);
      border: 0;
      outline: none;
      background: none;
      color: var(--ink);
      font: inherit;
    }

    & select {
      appearance: none;
      cursor: pointer;
      /* The native option list reads its colours from the select itself, not from the
         .field wrapper - leave this transparent and the popup falls back to white. */
      background-color: var(--surface);
    }
  }

  /* A full chrome reset. Leave the UA padding on and a bare <button> sits a few pixels
     right of everything else in the same column. */
  button {
    padding: 0;
    border: 0;
    background: none;
    font: inherit;
    color: inherit;
    text-align: inherit;
    cursor: pointer;
  }

  /* The one action a screen is really about. */
  .btn {
    appearance: none;
    display: flex;
    justify-content: center;
    align-items: center;
    gap: var(--step-2);
    width: 100%;
    min-height: var(--tap);
    padding: var(--step-3) var(--step-4);
    border: 0;
    border-radius: var(--radius-md);
    background: var(--accent);
    color: var(--on-accent);
    font-weight: 600;
    box-shadow: var(--lift);
    transition: translate var(--quick);

    &:active {
      translate: 0 1px;
    }

    &:disabled {
      opacity: 0.55;
      box-shadow: none;
      cursor: default;
    }
  }

  /* Everything secondary: a label you can press, not another rectangle. */
  .link {
    display: inline-flex;
    align-items: center;
    gap: var(--step-1);
    padding: var(--step-2);
    border: 0;
    background: none;
    color: var(--accent);
    font-size: var(--size-sm);
    font-weight: 600;
    text-decoration: none;

    &:disabled {
      color: var(--muted);
      cursor: default;
    }
  }

  /* A switch, not a checkbox: it reads as on/off at a glance and needs no border. */
  .switch {
    --track-w: 42px;
    --track-h: 24px;
    --knob-inset: 3px;

    appearance: none;
    position: relative;
    inline-size: var(--track-w);
    block-size: var(--track-h);
    flex: none;
    margin: 0;
    border-radius: var(--radius-pill);
    background: var(--line);
    cursor: pointer;
    transition: background var(--quick);

    &::after {
      content: "";
      position: absolute;
      inset: var(--knob-inset) auto var(--knob-inset) var(--knob-inset);
      aspect-ratio: 1;
      border-radius: 50%;
      background: var(--surface);
      transition: translate var(--quick);
    }

    &:checked {
      background: var(--accent);

      &::after {
        translate: calc(var(--track-w) - var(--track-h)) 0;
      }
    }
  }

  .ok {
    color: var(--ok);
  }

  .bad {
    color: var(--bad);
  }
}
""";

    /// <summary>
    /// Lucide, inlined. Round caps on a 24px grid, which is the same language as the
    /// rounded corners above - and it keeps the app working with no internet.
    /// </summary>
    public const string Sprite = """
<svg class="sprite" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
  <symbol id="i-upload" viewBox="0 0 24 24"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><path d="m17 8-5-5-5 5"/><path d="M12 3v12"/></symbol>
  <symbol id="i-lock" viewBox="0 0 24 24"><rect width="18" height="11" x="3" y="11" rx="2"/><path d="M7 11V7a5 5 0 0 1 10 0v4"/></symbol>
  <symbol id="i-check" viewBox="0 0 24 24"><path d="M20 6 9 17l-5-5"/></symbol>
  <symbol id="i-x" viewBox="0 0 24 24"><path d="M18 6 6 18"/><path d="m6 6 12 12"/></symbol>
  <symbol id="i-wifi" viewBox="0 0 24 24"><path d="M12 20h.01"/><path d="M2 8.82a15 15 0 0 1 20 0"/><path d="M5 12.86a10 10 0 0 1 14 0"/><path d="M8.5 16.43a5 5 0 0 1 7 0"/></symbol>
  <symbol id="i-folder" viewBox="0 0 24 24"><path d="M20 20a2 2 0 0 0 2-2V8a2 2 0 0 0-2-2h-7.9a2 2 0 0 1-1.69-.9L9.6 3.9A2 2 0 0 0 7.93 3H4a2 2 0 0 0-2 2v13a2 2 0 0 0 2 2Z"/></symbol>
  <symbol id="i-copy" viewBox="0 0 24 24"><rect width="14" height="14" x="8" y="8" rx="2"/><path d="M4 16c-1.1 0-2-.9-2-2V4c0-1.1.9-2 2-2h10c1.1 0 2 .9 2 2"/></symbol>
  <symbol id="i-cog" viewBox="0 0 24 24"><path d="M12.22 2h-.44a2 2 0 0 0-2 2v.18a2 2 0 0 1-1 1.73l-.43.25a2 2 0 0 1-2 0l-.15-.08a2 2 0 0 0-2.73.73l-.22.38a2 2 0 0 0 .73 2.73l.15.1a2 2 0 0 1 1 1.72v.51a2 2 0 0 1-1 1.74l-.15.09a2 2 0 0 0-.73 2.73l.22.38a2 2 0 0 0 2.73.73l.15-.08a2 2 0 0 1 2 0l.43.25a2 2 0 0 1 1 1.73V20a2 2 0 0 0 2 2h.44a2 2 0 0 0 2-2v-.18a2 2 0 0 1 1-1.73l.43-.25a2 2 0 0 1 2 0l.15.08a2 2 0 0 0 2.73-.73l.22-.39a2 2 0 0 0-.73-2.73l-.15-.08a2 2 0 0 1-1-1.74v-.5a2 2 0 0 1 1-1.74l.15-.09a2 2 0 0 0 .73-2.73l-.22-.38a2 2 0 0 0-2.73-.73l-.15.08a2 2 0 0 1-2 0l-.43-.25a2 2 0 0 1-1-1.73V4a2 2 0 0 0-2-2z"/><circle cx="12" cy="12" r="3"/></symbol>
  <symbol id="i-chevron" viewBox="0 0 24 24"><path d="m6 9 6 6 6-6"/></symbol>
  <symbol id="i-shield" viewBox="0 0 24 24"><path d="M20 13c0 5-3.5 7.5-7.66 8.95a1 1 0 0 1-.67-.01C7.5 20.5 4 18 4 13V6a1 1 0 0 1 1-1c2 0 4.5-1.2 6.24-2.72a1.17 1.17 0 0 1 1.52 0C14.51 3.81 17 5 19 5a1 1 0 0 1 1 1z"/></symbol>
  <symbol id="i-phone" viewBox="0 0 24 24"><rect width="14" height="20" x="5" y="2" rx="2"/><path d="M12 18h.01"/></symbol>
</svg>
""";
}
