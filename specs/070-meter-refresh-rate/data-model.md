# Data model

Settings.MeterRefreshHz: integer, default 30; normalization accepts only 60 or returns 30.
Existing SettingsService serialization and Changed event persist/apply it. No project data.
MeterRefreshCadence stores current rate and next monotonic deadline; not persisted.
