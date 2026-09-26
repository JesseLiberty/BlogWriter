# Revision Window Availability Contract

This contract defines when the existing Revision request field is editable. It does not define or alter Go submission routing.

| Workspace state | Query content | Draft | Revision field content | Revision field |
|---|---|---|---|---|
| Editable New | Empty or whitespace-only | Absent | Any | Disabled |
| Editable New | Non-whitespace | Absent | Empty or populated | Enabled |
| Editable Draft | Empty or whitespace-only | Any | Any | Disabled |
| Editable Draft | Non-whitespace | Any | Empty or populated | Enabled |
| Go processing | Any | Any | Any | Disabled |
| List loading or List mode | Any | Any | Any | Disabled |
| Ended | Any | Any | Any | Disabled/not rendered |

The availability changes as soon as the query text changes between empty and non-empty. Existing operation and mode lockouts take precedence over the query condition.