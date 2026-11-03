# EN/RU manual update prepared for nota-docs

The sibling `C:/Dev/nota-docs` repository is absent. These are ready-to-apply manual additions, not published website changes. Add them to the Arrangement and Piano Roll pages and main-window overview; regenerate the shortcuts reference from Preferences, retake screenshots and run the documentation build/check when that repo is available.

## English

Arrangement and Piano Roll have the same five-tool toolbar. Each editor remembers its active tool; both share the selected waveform and amplitude/cycle/phase settings.

With an editor focused, use the bare top-row keys:

| Key | Tool |
| --- | --- |
| 1 | Select Tool |
| 2 | Pencil Tool |
| 3 | Eraser Tool |
| 4 | Waveform Draw Tool |
| 5 | Nota Warp Tool |

Press **4** to enter Waveform Draw with the last shape. Press it again to cycle **Sine → Triangle → Sawtooth → Square → Freehand → Sine**. Holding a key does not repeat. Both waveform icons update immediately, and choosing a shape in either dropdown changes the shared selection. Switching tools keeps that shape. The same shortcuts work in the detached Piano Roll. Text/numeric fields, dialogs and plugin windows keep their own input; Cmd/Ctrl+1–5 retain their existing grid commands.

In Piano Roll, **Pencil** creates a note using the remembered length/velocity; drag to adjust duration and pitch, or click to keep the default length. Clip bounds constrain the note. **Eraser** removes the clicked editable note or controller point. **Select** retains the original selection, move, resize and modifier gestures.

For waveform drawing, choose an existing controller lane such as **CC1** or **CC11**, then drag across it. The arrow beside Waveform Draw opens shape, amplitude, cycle length and phase choices. Drawing changes only that lane within the swept interval, uses its range and current grid, and leaves notes/other controllers intact. Use a finer grid or longer cycle for short periodic shapes. Switch-controller lanes retain their on/off behavior. **Esc** or a tool change cancels an unfinished stroke; a completed stroke is one Undo action. Drawing over the note grid or incompatible Velocity/MPE lanes with Waveform Draw makes no MIDI edit.

Arrangement keeps its MIDI-clip Pencil, clip/point Eraser and track/return/master automation drawing. Arrangement does not have MIDI CC lanes.

**Nota Warp** currently provides a muted red ruler tint, **WARP** badge and grid-line hover feedback in either editor. Shared tempo-map/Tempo Track editing is not implemented, so no warp anchors are created and no tempo, note position or audio data is changed. Existing ruler navigation remains. Tool/shape choices are session UI state; notes/controllers use the existing project save format.

## Русский

В Arrangement и Piano Roll используется одна и та же панель из пяти инструментов. Каждый редактор запоминает свой активный инструмент, а форма волны и настройки амплитуды, цикла и фазы общие.

Когда фокус в редакторе, используйте верхний ряд цифр без модификаторов:

| Клавиша | Инструмент |
| --- | --- |
| 1 | Select Tool |
| 2 | Pencil Tool |
| 3 | Eraser Tool |
| 4 | Waveform Draw Tool |
| 5 | Nota Warp Tool |

Первое нажатие **4** включает рисование с последней выбранной формой. Следующие нажатия перебирают **Sine → Triangle → Sawtooth → Square → Freehand → Sine**. Удержание клавиши не запускает повторное переключение. Значки в обеих панелях обновляются сразу; форма, выбранная в любом выпадающем меню, становится общей. Смена инструмента её не сбрасывает. В отдельном окне Piano Roll работают те же клавиши. Текстовые и числовые поля, диалоги и окна плагинов сохраняют собственный ввод; Cmd/Ctrl+1–5 по-прежнему управляют сеткой.

В Piano Roll **Pencil** создаёт ноту с последними длительностью и велосити. Протягивание меняет длительность и высоту, а простой щелчок оставляет длительность по умолчанию. Нота ограничена границами клипа. **Eraser** удаляет конкретную редактируемую ноту или точку контроллера. **Select** сохраняет привычные выделение, перемещение, изменение границ и жесты с модификаторами.

Для рисования кривой выберите существующую полосу контроллера, например **CC1** или **CC11**, и протяните по ней. Стрелка рядом с Waveform Draw открывает выбор формы, амплитуды, длины цикла и фазы. Меняется только выбранный контроллер в охваченном интервале, с учётом диапазона и сетки; ноты и другие контроллеры остаются прежними. Для коротких циклов выбирайте более мелкую сетку или увеличивайте цикл. Переключаемые контроллеры сохраняют состояния вкл/выкл. **Esc** или смена инструмента отменяет незавершённый жест; завершённый жест отменяется одним Undo. Waveform Draw не меняет MIDI при рисовании по сетке нот или несовместимым полосам Velocity/MPE.

В Arrangement сохраняются рисование MIDI-клипов через Pencil, удаление клипов/точек через Eraser и рисование автоматизации дорожек, возвратов и мастера. Полос MIDI CC в Arrangement пока нет.

**Nota Warp** пока добавляет только приглушённую красную заливку линейки, метку **WARP** и подсветку линии сетки под курсором. Общий движок карты темпа и Tempo Track ещё не реализован: якоря Warp не создаются, темп, положение нот и аудиоданные не меняются. Привычная навигация по линейке остаётся доступной. Инструмент и форма волны хранятся в состоянии текущего сеанса; ноты и контроллеры сохраняются в существующем формате проекта.
