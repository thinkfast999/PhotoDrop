# UI Design Skill

This design document aims to deter default AI agent 'slop' visuals and improve the user experience.

## Brief crash course in design

'An aesthetic' or a 'style' is an emergent symptom created by pattern-seeking behaviors of the human mind. Establishing arbitrary constraints on elements' visual attributes is mandatory for a pleasant experience; Typograhic rules for text content, container line & column gaps, a constrained palette, etc...

We will keep this in mind at all times when designing our UI - what kind of cognitive patterns do we enforce, and when? Some are more useful than others.

## Layout guidelines

1. Forbidden: Stacked Cards
    A bordered container may not have a child at any depth that also renders its own borders. This is by far the biggest sin of modern LLMs - we avoid this at all costs. A card can only render on top of the page background color, never inside another card.
2. Icons
    Use an icon library, never Emojis. Some icon libraries will harmonize better with the rest of the interface. Take this simplified example: Pick the round icons for the UI that uses rounded borders, and square/boxy icons for those without rounded borders. Apply this to all visual attributes and you get the idea.
3. Style reuse
    Never hardcode magic values. Define CSS variables that you can reuse to prevent drift when things change.
4. Pressable elements
    Don't style all interactive elements like rectangular buttons - you can use clickable text labels too.
5. Smart `display` choices
    Choose appropriate `display` types depending on the layout - Not everything should to be a flexbox.

## User-facing text content guidelines

1. User-facing text
    - BREVITY: Verbosity is hell, brevity is heaven. User attention span isn't cheap, and that's why you should cut all padding.
    - RELEVANCE:
        1. Does the text content you are about to add need to exist at all? Second-guess yourself every time.
        2. If it does: Explanatory text content must serve a *visitor of the app*  - NOT the developer that prompted you! Leaking context related to your prompt into the UI is a sin punishable by 20 hours in the torture tower.
    - LANGUAGE: Cut technical details or explainers about internal logic that only serve to confuse visitors. Example Scenario: You are designing a settings menu where every field is succeeded by a tooltip label. You decide to add a new field, and you make its tooltip label something that *briefly states what it is* - **not** system documentation.

## CSS

Use modern CSS features, but make sure they are supported in all major browsers. No, that instruction does not mean that you should take the safe way out and fall back to using legacy CSS for everything. Here are examples of modern, supported CSS vanilla features:

- Nested CSS rules. Example:
    ```css
    .foo {
        /* .foo .bar */
        .bar {
            /* .foo .bar > .baz */
            & > .baz {

            }
        }
    }
    ```

- [https://developer.mozilla.org/en-US/docs/Web/CSS/Reference/Selectors/:not](:not() CSS rule)

- [https://developer.mozilla.org/en-US/docs/Web/CSS/Reference/Selectors/:has](:has() CSS rule)

- [https://developer.mozilla.org/en-US/docs/Web/CSS/Reference/Selectors/:is](:is() CSS rule)

- [https://developer.mozilla.org/en-US/docs/Web/CSS/Reference/Values/anchor](anchor() CSS function)
    The `anchor()` CSS function is extremely useful. You can use `anchor-name` to specify a target element's anchor name so that it can be referenced as a CSS variable using the property `position-anchor`. `position-area: <` is used to change where an element renders releative to its anchor target. There's also `position-try`, `position-try-fallbacks` and `position-try-order`. Read more about anchors here: https://developer.mozilla.org/en-US/docs/Web/CSS/Reference/Values/anchor

- [https://developer.mozilla.org/en-US/docs/Web/CSS/Reference/At-rules/@layer](@layer) CSS cascade layer @ rule.
    > The @layer CSS at-rule is used to declare a cascade layer and can also be used to define the order of precedence in case of multiple cascade layers.

