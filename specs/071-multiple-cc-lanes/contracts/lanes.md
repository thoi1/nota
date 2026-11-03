# Lane contract

+ appends an extra controller editor. A lane's picker changes only that view. - cancels
its gesture and removes its view/gutter column. Neither operation writes controller data.
StoreController(controllerId, points) is the existing single undo/commit boundary.
All visible lanes share BeatToX/XToBeat, active-clip mapping and cache invalidation.
