<!-- SPDX-License-Identifier: AGPL-3.0-only -->
# Manual handoff — Piano roll workspace improvements

The sibling `../nota-docs` checkout is unavailable. Apply these paired updates to the
Piano roll, main-window/detail-panel and shortcuts pages, then build the manual and
retake affected screenshots. No manual site build is claimed here.

## English

Double-click a MIDI clip in Arrangement to fill the editing workspace with its piano
roll. The menu and transport stay available. Click **Back to Arrangement** (or use the
existing **⌘⇧F / Ctrl+Shift+F** workspace command) to restore the previous layout.
A detached clip editor temporarily moves into the main window, then returns to the same
window. Devices and Mixer take no workspace height while hidden; their audio keeps running.

In a detached editor, close the entire lower **Devices** area with its **×** button.
Choose **View → Show devices** to reopen it. The docked panel keeps its existing close
button and **View → Toggle detail panel**. Returning from the piano workspace preserves
manually closed Devices.

Drag the divider above **Velocity** or an individual **CC lane** up to make that lane
taller, down to shorten it. Heights are remembered when you reopen the editor or Nota.
Extra lanes scroll when space is limited. Resizing changes no notes, controller values
or undo history. Velocity stems align with note starts. Horizontal zoom now reaches
3,200 pixels per beat, eight times the former maximum, with the same minimum and pointer anchor.

Shortcuts: **Double-click clip** — MIDI fills the piano workspace, audio opens its normal
clip editor; multiple selected MIDI clips open together. **Drag a lane divider** — resize
that lane and remember its height.

## Русский

Дважды щёлкните MIDI-клип в Arrangement: Piano Roll займёт рабочую область, а меню и
транспорт останутся доступны. **Back to Arrangement** (или **⌘⇧F / Ctrl+Shift+F**) вернёт
прежнюю раскладку. Если редактор был в отдельном окне, он временно переместится в главное,
а при возврате откроется в том же окне. Скрытые Devices и Mixer не занимают высоту;
инструменты и эффекты продолжают звучать.

В отдельном редакторе кнопка **×** в заголовке **Devices** скрывает всю нижнюю область,
включая инструмент, Sends и место для добавления устройств. **View → Show devices**
открывает её снова. У встроенной панели остаются прежняя кнопка закрытия и команда
**View → Toggle detail panel**. Возврат из Piano Roll сохраняет ручное закрытие Devices.

Потяните разделитель над **Velocity** или отдельной **CC lane** вверх, чтобы увеличить
высоту, либо вниз, чтобы уменьшить её. Высота сохраняется при открытии редактора и после
перезапуска Nota. Если места мало, дополнительные дорожки прокручиваются. Изменение высоты
не меняет MIDI-ноты, значения контроллеров или историю отмены. Столбики velocity точно
совпадают с началом нот. Максимальное горизонтальное увеличение теперь составляет
3 200 пикселей на долю — в восемь раз больше; минимум и привязка к указателю сохранены.

Справочник жестов: **Double-click clip** — MIDI открывает Piano Roll на всю рабочую
область, аудио — обычный редактор; выбранные MIDI-клипы открываются вместе.
**Drag a lane divider** — изменить и запомнить высоту отдельной дорожки.
