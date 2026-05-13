# Smoke Test — Per-Chart Filter Feature

This guide covers testing the new per-chart filter functionality in LQ.

## Setup

1. Start the app: `dotnet run --project LQ/LQ.csproj`
2. Upload a CSV with at least one categorical column (string, bool, or list field)
3. Create 2-3 charts by dragging column headers onto the canvas

## Test Cases

### 1. Filter Block Creation

**Test:** Convert a chart to a filter block
- [ ] Click the "Turn into" menu on a single-field chart
- [ ] Select "Filter"
- [ ] Verify the chart disappears and a filter block appears with the field name
- [ ] Verify filter block has correct variant:
  - Small cardinality (≤ 7): shows button pills for each value
  - Large cardinality (> 7): shows search input
- [ ] Delete the filter block; canvas should not break

**Test:** Create a filter block from scratch (if UI supports it)
- [ ] Verify filter block can be created via `/` command or menu
- [ ] Verify it starts with empty state "Pick a field"

### 2. Filter Selection & Scope

**Test:** Configure which charts a filter applies to
- [ ] Click the filter block to select it (should show blue glow)
- [ ] Right panel should switch to "filter" mode
- [ ] Right panel shows list of charts on canvas
- [ ] Checkbox for each chart shows current "applied to" state
- [ ] Toggle a chart checkbox
- [ ] Verify the chart immediately updates with filtered data (or unchanged if filter is empty)

**Test:** Deselect filter
- [ ] Click elsewhere on canvas
- [ ] Filter should lose blue glow
- [ ] Right panel should return to canvas options

### 3. Filter Interaction

**Test:** Button variant (≤ 7 values)
- [ ] Click a button/pill
- [ ] Button should show visual "active" state
- [ ] All connected charts should update with filtered data
- [ ] Click another button (multi-select)
- [ ] Both buttons active, charts show data matching EITHER value
- [ ] Click a button again to deselect
- [ ] Click "Clear" button to reset all selections

**Test:** Search variant (> 7 values)
- [ ] Type in search box
- [ ] Matching values appear as chips below
- [ ] Click a chip to add it to filter
- [ ] Chip appears above search box
- [ ] Charts update to show filtered data
- [ ] Click the X on a chip to remove it
- [ ] Click "Clear" to reset

### 4. Multi-Chart Filtering

**Test:** One filter on multiple charts
- [ ] Create a filter block with 2+ charts selected in the right panel
- [ ] Select a value
- [ ] All selected charts should show filtered data
- [ ] Uncheck one chart in right panel
- [ ] That chart should revert to unfiltered data
- [ ] Other charts should still show filtered data

**Test:** Multiple filters on one chart
- [ ] Create 2 filter blocks, both pointing to the same chart
- [ ] Select a value in filter #1 → chart updates
- [ ] Select a value in filter #2 → chart should show data matching BOTH filters (intersection)
- [ ] Deselect one filter's value → chart updates to show data matching only remaining filter

### 5. State Persistence

**Test:** Filter configuration persists on reload
- [ ] Set up filters, configure which charts they apply to
- [ ] Refresh page (Ctrl+R or F5)
- [ ] Filter blocks should still exist in same positions
- [ ] "Applied to" checkboxes should match previous state
- [ ] **Selected values should be EMPTY** (not persisted)
- [ ] Charts should be unfiltered (no selections active)

**Test:** Filter position and size persist
- [ ] Drag a filter block to a new position
- [ ] Resize the filter block
- [ ] Refresh page
- [ ] Filter block position and size should match

### 6. Data Integrity

**Test:** Correct filtering logic
- [ ] Apply a string filter to a chart with string data
- [ ] Verify chart only shows rows matching the selected values
- [ ] Apply a numeric filter (buttons with integers)
- [ ] Verify numeric values are correctly filtered
- [ ] Apply a list field filter (comma-separated values in CSV)
- [ ] Verify filtering works correctly for list fields

**Test:** Filter with changed data
- [ ] Apply a filter that returns data
- [ ] Apply a filter that returns NO data
- [ ] Chart should show empty state (no data to render)
- [ ] Remove filter
- [ ] Chart should return to showing all data

### 7. Chart Type Updates

**Test:** Area chart stacking preserved
- [ ] Create a stacked area chart
- [ ] Add a per-chart filter
- [ ] Select filter values
- [ ] Chart should remain a stacked area (not convert to line with fill)
- [ ] Remove filter values
- [ ] Chart should still be stacked area

**Test:** Scatter plot colors correct
- [ ] Create a scatter chart with multiple series
- [ ] Add a per-chart filter connected to it
- [ ] Select filter values
- [ ] Scatter points should have:
  - Semi-transparent background color (per series)
  - Visible colored border (matching background)
  - No grey borders or borders from other datasets
- [ ] Unfilter (remove filter values)
- [ ] Scatter colors should be clean (no grey borders in legend)

**Test:** Other chart types
- [ ] Test filters on: bar, line, pie, mixed charts
- [ ] Verify colors, borders, stacking are correct after filtering

### 8. Filter Limits

**Test:** Large cardinality fields
- [ ] Create a filter on a field with 50+ distinct values
- [ ] Verify search variant loads (not buttons)
- [ ] Search for a value
- [ ] Filter should work correctly
- [ ] Verify no performance issues

---

## Edge Cases

- [ ] Filter a field with NULL/blank values
- [ ] Filter on a field after changing its type
- [ ] Delete a chart that a filter is connected to
- [ ] Copy a filter block (if supported)
- [ ] Copy a chart with filters applied to another canvas
- [ ] Filter with special characters in values (quotes, commas, etc.)

---

## Regression Tests

- [ ] Table-wide filters still work (session-based FilterTable endpoint)
- [ ] Table-wide filter + per-chart filter don't interfere
- [ ] Charts without filters still render correctly
- [ ] Zoom overlay works on filtered charts
- [ ] Chart type selector in zoom works on filtered charts
- [ ] Canvas persistence (save/reload) still works with filter blocks

---

## Performance Checks

- [ ] Filter with 100+ distinct values loads quickly
- [ ] Filtering a chart with 10K+ rows is responsive
- [ ] Multiple filters on multiple charts don't lag UI
- [ ] No console errors (F12 DevTools)
