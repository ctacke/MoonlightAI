# Code Cleanup Workload (In Process)

Performs automated refactoring and code quality improvements.

**Cleanup Operations:**
- Remove unused variables
- Remove unused using statements
- Convert public fields to properties
- Reorder private fields
- Extract magic numbers to constants
- Simplify boolean expressions
- Remove redundant code
- Simplify string operations
- Use expression-bodied members

**Configuration:**
```json
{
  "Workload": {
    "CodeCleanup": {
      "Enabled": true,
      "Operations": [
        "RemoveUnusedVariables",
        "RemoveUnusedUsings",
        "ConvertFieldsToProperties"
      ]
    }
  }
}
```
