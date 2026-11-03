# Manual additions to apply when nota-docs is available

These instructions describe implemented behavior. They have not been applied to the absent sibling website repository. Add the section to the Arrangement manual and its Russian twin, update the main-window tool overview, regenerate the shortcuts page from Preferences, retake the affected app screenshots, then run the documentation build/check.

## English

Choose an editing tool beside **Snap** and **Automation** in the main Arrangement toolbar. One tool stays active at a time:

- **Select Tool**: select, move and resize clips with the existing gestures.
- **Pencil Tool**: click or drag an empty instrument-track gap to create a MIDI clip using the current grid. Drag either direction; the clip stops at its neighbors. Show **Automation** to draw an envelope instead.
- **Eraser Tool**: click a MIDI/audio clip or an automation point to delete that item. Undo restores it.
- **Waveform Draw Tool**: show **Automation**, then drag across a track, return or master lane. The arrow beside the tools chooses **Sine**, **Triangle**, **Sawtooth**, **Square** or **Freehand**, plus amplitude, cycle length and phase. The press position sets the center value; the lane limits the result. Choose a finer grid or longer cycle if a short waveform is undersampled. The tool edits automation points, never audio samples.
- **Nota Warp Tool**: currently selection-only. Ruler tempo-grid editing requires a shared tempo-map/Tempo Track engine that is not yet available; selecting the tool does not change tempo or audio.

Each completed drawing stroke is one Undo action. Press **Esc** during drawing to discard the preview. Tool changes also discard an unfinished preview. Automation visibility remains independently controlled. MIDI CC lanes belong to the piano roll; Arrangement does not have CC lanes.

Alt bypasses snapping for existing Arrangement clip movement. Arrangement clip drags do not currently implement Alt-drag duplication or Alt + right-click erase.

## Русский

Выберите инструмент рядом с **Snap** и **Automation** на верхней панели Arrangement. Одновременно активен один инструмент:

- **Select Tool** — выделение, перемещение и изменение границ клипов привычными жестами.
- **Pencil Tool** — щёлкните или протяните по свободному месту инструментальной дорожки, чтобы создать MIDI-клип по текущей сетке. Тянуть можно в обе стороны; соседние клипы ограничивают длину. Включите **Automation**, чтобы рисовать огибающую.
- **Eraser Tool** — щёлкните MIDI- или аудиоклип либо точку автоматизации, чтобы удалить именно этот элемент. Undo восстанавливает его.
- **Waveform Draw Tool** — включите **Automation** и протяните по полосе дорожки, возврата или мастера. Стрелка рядом с инструментами открывает **Sine**, **Triangle**, **Sawtooth**, **Square**, **Freehand**, а также амплитуду, длину цикла и фазу. Высота начального нажатия задаёт центр; значения ограничены диапазоном параметра. Если сетка слишком редкая для короткого цикла, уменьшите шаг сетки или увеличьте цикл. Инструмент меняет точки автоматизации, а не аудиосэмплы.
- **Nota Warp Tool** — пока доступно только выделение инструмента. Для правки темповой сетки на линейке нужен общий движок карты темпа и Tempo Track; сейчас его нет. Выбор инструмента не меняет темп или звук.

Завершённый жест рисования отменяется одним Undo. **Esc** во время рисования отменяет предпросмотр; смена инструмента также сбрасывает незавершённый жест. Видимость Automation переключается отдельно. Полосы MIDI CC остаются в piano roll; в Arrangement таких полос пока нет.

Alt отключает привязку при существующем перемещении клипов Arrangement. Дублирование клипов через Alt-перетаскивание и стирание через Alt + правый щелчок в Arrangement пока не реализованы.
