# Custom Element Constructors — Decompiled Analysis

Reference for replicating the 13 custom UI element constructors in the WebGL factory system.
Source: `Il2CppDumper/dump.cs` (1.98M lines) + Ghidra `decompiled_all.c` analysis.

## Inheritance Hierarchy

```
VisualElement
  +-- DofusVisualElement (Core.UILogic.Components.Figma)
  |     +-- DofusBindableElement
  |     |     +-- DofusButtonCustom    USS: "button"
  |     |     +-- PlayButton           USS: "playButton"
  |     |     +-- TextInput            USS: "textInput"
  |     |     +-- TextInputToValidate  USS: "textInputToValidate"
  |     |     +-- Dropdown             USS: "dropdown"
  |     |     +-- DropdownButton       USS: "dropdownButton"
  |     +-- DofusIcon                  CSS vars: --icon, --shadow
  |     +-- CircularGauge             Custom mesh + label + icon
  +-- WindowFigma                      USS: "window"
  +-- Divider (: BindableElement)      USS: "divider"
  +-- Label
        +-- DofusLabel (Core.UILogic.Components)  CSS vars: --shadow, --text-case
```

## 1. DofusVisualElement (BASE)

**Fields:** `m_shadowContent` (VE), `m_gapValue` (int), `m_shadowValue` (List<ShadowData>)

**Constructor:**
- Registers `GeometryChangedEvent` callback
- Registers `CustomStyleResolvedEvent` for `--gap` and `--shadow` CSS variables
- Creates shadow content wrapper if `hasShadow = true`

**UXML attrs:** `gap-value` (int), `has-shadow` (bool)

## 2. DofusBindableElement

**Extends:** DofusVisualElement

**Fields:** `binding` (IBinding), `bindingPath` (string)

**Constructor:** Calls base DVE ctor + sets up data binding. No visual changes.

**UXML attrs:** `binding-path` (string)

## 3. DofusButtonCustom

**Extends:** DofusBindableElement | **USS:** `button`

**Child hierarchy created in constructor:**
```
this (Button + .button)
  +-- container (DVE, "button__container")
      +-- iconLeftContent (VE)
      |   +-- iconLeft (DofusIcon)
      +-- imgLeftContent (VE)
      |   +-- imgLeft (DVE)
      +-- textContainer (DofusLabel, "button__label")
      +-- imgRightContent (VE)
      |   +-- imgRight (DVE)
      +-- iconRightContent (VE)
          +-- iconRight (DofusIcon)
```
Also creates `ExtendedClickable` manipulator.

**UXML attrs:** `text`, `image-url`, `icon` (FigmaIcons), `main-style` (primary/secondary/tertiary/link/floating), `status` (normal/negative/strong/light/dark), `icon-position`, `icon-size`, `icon-type`, `enable-rich-text`, `display-tooltip-when-elided`

## 4. PlayButton

**Extends:** DofusBindableElement | **USS:** `playButton`

**Child hierarchy (layered):**
```
this (Button + .playButton)
  +-- shadowEffect (VE)
  +-- shadowEffectHover (VE)
  +-- shadowEffectPressed (VE)
  +-- background (VE)
  +-- border (VE)
  +-- lightEffect (VE)
  +-- lightEffectHover (VE)
  +-- lightEffectPressed (VE)
  +-- gems (VE)
  +-- label (DofusLabel)
  +-- chevronIcon (DofusIcon)
```
Also creates `ExtendedClickable` + mouse/keyboard event handlers + DOTween animations for hover/press states.

**UXML attrs:** inherits DofusBindableElement (no additional)

## 5. WindowFigma

**Extends:** VisualElement | **USS:** `window`

**Child hierarchy:**
```
this (VE + .window)
  +-- shadow (VE)
  +-- background (VE)
  +-- header (HeaderWindow — close/help buttons, title)
  +-- content (VE) <- returned as contentContainer
```
Also creates movable manipulator if `isMovable = true`.

**UXML attrs:** `with-title-bar`, `hide-title-bar`, `is-movable`, `show-close-button`, `is-modal`, `has-shadow`, `show-help-button`, `no-ornament-header` (bool); `header-style` (enum); `title`, `subtitle-text`, `img-url`, `shortcut-value` (string); `help-icon` (FigmaIcons)

## 6. Divider

**Extends:** BindableElement | **USS:** `divider`

**Child hierarchy:**
```
this (VE + .divider)
  +-- lineLeft (DVE)
  +-- diamondContainer (DVE)
  |   +-- diamondIcon (DofusIcon)
  +-- textLabel (DofusLabel)
  +-- lineRight (DVE)
```

**UXML attrs:** `text` (string), `diamond` (FigmaIcons), `horizontalPadding` (int)

## 7. TextInput

**Extends:** DofusBindableElement | **USS:** `textInput`

**State classes:** `errorTrue`/`errorFalse`, `activeTrue`/`activeFalse`, `multiline`, `hasEraseButton`

**Child hierarchy:**
```
this (TextField + .textInput)
  +-- content (DVE)
      +-- iconLeftContainer (VE)
      |   +-- iconLeft (DofusIcon)
      +-- textContainer (VE)
      |   +-- textField (DofusTextFieldCustom)
      +-- iconRightContainer (VE)
      |   +-- iconRight (DofusIcon)
      +-- eraseButton (DofusButtonCustom, if hasEraseButton)
  +-- infosContainer (VE)
  |   +-- errorLabel (DofusLabel)
  |   +-- maxLengthLabel (DofusLabel)
  +-- placeholderLabel (DofusLabel)
```

**UXML attrs:** `text-input-style`, `is-multiline`, `is-numbers-only`, `is-password`, `has-erase-button`, `is-read-only` (bool); `content-type`, `error-condition` (enum); `icon` (FigmaIcons); `custom-regex`, `error-text`, `placeholder-text` (string); `min-length`, `max-length`, `min-value`, `max-value` (int)

## 8. TextInputToValidate

**Extends:** DofusBindableElement | **USS:** `textInputToValidate`

**Child hierarchy:**
```
this (TextField + .textInputToValidate)
  +-- textField (DofusTextFieldCustom)
  +-- maxLengthContainer (VE)
  |   +-- maxLengthLabel (DofusLabel)
  +-- errorLabel (DofusLabel)
  +-- placeholderLabel (DofusLabel)
  +-- cta (DVE)
      +-- buttonEdit (DofusButtonCustom)
      +-- buttonValidate (DofusButtonCustom)
      +-- buttonCancel (DofusButtonCustom)
```
Two modes: consultation (read-only + Edit) / editing (editable + Validate/Cancel).

**UXML attrs:** `text-input-style` (enum); `button-edit-icon`, `button-validate-icon`, `button-cancel-icon` (FigmaIcons); `is-multiline`, `is-numbers-only` (bool); `error-text`, `placeholder-text` (string); `min-length`, `max-length` (int)

## 9. Dropdown

**Extends:** DofusBindableElement | **USS:** `dropdown`

**Child hierarchy:**
```
this (VE + .dropdown)
  +-- dropdown (DVE, button area)
      +-- captionIconContainer (DVE)
      +-- captionLabel (DofusLabel)
      +-- choiceLabel (DofusLabel)
      +-- chevronIcon (DofusIcon)
  +-- scrollContainer (DVE)
      +-- listElementContent (ScrollView)
```
Parses `items` string to populate list. Click handler shows/hides dropdown.

**UXML attrs:** `dropdown-size`, `dropdown-type` (enum); `caption-text`, `items` (string, comma-separated); `icon` (FigmaIcons); `item-content-max-height` (float); `hide-value`, `has-mouse-wheel` (bool)

## 10. DropdownButton

**Extends:** DofusVisualElement | **USS:** `dropdownButton`

**Child hierarchy:**
```
this (VE + .dropdownButton)
  +-- button (DofusButtonCustom)
  +-- dropdown (IconDropdown)
```

**UXML attrs:** `button-text`, `button-image-url` (string); `button-icon` (FigmaIcons); `button-icon-position`, `button-icon-size`, `button-icon-type` (enums); `dropdown-content-max-height` (float)

## 11. DofusIcon

**Extends:** DofusVisualElement | **USS:** none specific

**No child elements.** Uses CSS variables:
- `--icon` -> sets background-image
- `--shadow` -> applies shadow
Registered `CustomStyleResolvedEvent` callback.

**UXML attrs:** `icon` (FigmaIcons enum), `prefers-big` (bool)

## 12. CircularGauge

**Extends:** DofusVisualElement

**Child hierarchy:**
```
this (VE + .CircularGauge)
  +-- label (DofusLabel)
  +-- dofusIcon (DofusIcon)
  +-- img (VE)
```
Registers `generateVisualContent` callback for custom mesh generation (draws circular progress arc).

**UXML attrs:** `progress`, `border-width`, `delimiter-perc` (float); `text`, `image-url` (string); `gauge-color`, `reach-color` (enum); `has-delimiters` (bool); `icon` (FigmaIcons)

## 13. DofusLabel

**Extends:** Label | **USS:** stored in `UssClassName` static

**No child elements.** Registers `CustomStyleResolvedEvent` for:
- `--shadow` (color + offset)
- `--text-height`
- `--text-case` (uppercase/lowercase transform)
- `--text-style`
- `--is-single-line`
Optionally adds `HyperlinkManipulator`.

**UXML attrs:** `is-single-line` (bool), `shadow-offset-x`, `shadow-offset-y` (int), `shadow-color` (Color)

## USS Class Mapping (Extracted from reconstructed .uss files)

Verified by searching 228 USS files. Source files in `C:\DofusWeb\Assets\UI\Utils\Style\`.

| Component | USS File | Main Class | Child Classes |
|---|---|---|---|
| DofusButtonCustom | Components.Button.uss | `.dofus-button` | `__label`, `__icon`, `-iconOnly__icon`, `-grown__label` |
| WindowFigma | Components.Window.uss | (none on root) | `.window__background`, `__border`, `__container-title-bar`, `__title-bar-headline`, `__close-button`, `__help-button`, `__content`, `__container_content`, `__modal_background` |
| TextInput | Components.InputField.uss | `.dofus-text-field` | `__icon`, `__text-field`, `__input`, `__cancel-button`, `__placeholder` |
| Dropdown | Components.Combobox.uss | `.combobox` | `__button`, `__downArrow`, `__upArrow`, `__scrollview`, `__item`, `__item__label`, `__arrowContainer`, `__button__content` |
| PlayButton | login.uss (screen-specific) | `.loginButton` | `#loginButton_container` (background-image: bigPlayButtonWhite) |
| DofusLabel | (none found) | — | — |
| DofusIcon | (none found) | — | — |
| Divider | (none found) | — | — |
| CircularGauge | (none found) | — | — |

**Variant classes (from Button.uss):** `.greenButton`, `.greenSmallButton`, `.greenLargeButton`, `.greenThinButton`, `.paginationButton`, `.default_green_button`

**Key finding:** PlayButton, DofusLabel, DofusIcon, Divider, CircularGauge have NO standalone USS rules. Their visual comes entirely from C# constructor code (inline styles, CSS variables, child elements). The login PlayButton uses screen-specific `.loginButton` class from `UIToolkit-Importing/USS/login.uss`.

## Factory Implementation

### Per-element USS loading (replicates AddStyleSheetPath from constructors)
| Factory | Loads USS | Source |
|---|---|---|
| F_DofusButtonCustom | `Core.UILogic.Components.Button` | Button.uss |
| F_WindowFigma | `Core.UILogic.Components.Window` | Window.uss |
| F_TextInput | `Core.UILogic.Components.InputField` | InputField.uss |
| F_TextInputToValidate | `Core.UILogic.Components.InputField` | InputField.uss |
| F_Dropdown | `Core.UILogic.Components.Combobox` | Combobox.uss |

### WebGLClickable manipulator (replaces ExtendedClickable)
| Factory | Has manipulator | Original |
|---|---|---|
| F_DofusButtonCustom | Yes — AddClickable | ExtendedClickable |
| F_PlayButton | Yes — AddClickable | ExtendedClickable |
| F_DropdownButton | Yes — AddClickable (on inner button) | ExtendedClickable |

### Key Patterns
1. **Inheritance chain:** Always call parent setup first (DVE -> DBE -> specific type)
2. **USS classes:** Add the REAL USS class (e.g., `.dofus-button`) AND the PascalCase type name (for CSS type->class transformation)
3. **Child elements:** Use USS-verified class names (e.g., `.dofus-button__label`, `.window__close-button`)
4. **Per-element USS:** LoadComponentUSS replicates what AddStyleSheetPath does in constructors
5. **Manipulator:** WebGLClickable replaces ExtendedClickable — prevents UIToolkit default hover
6. **CSS variables:** `--gap`, `--shadow`, `--icon`, `--text-case`, `--text-height` — not yet replicated (need CustomStyleResolvedEvent handlers)
