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
    "SolutionPath": "MyProject.sln",
    "IgnoreProjects": ["MyProject.Tests.csproj"],
    "CodeCleanup": {
      "Options": {
        "RemoveUnusedVariables": true,
        "RemoveUnusedUsings": true,
        "ConvertPublicFieldsToProperties": true,
        "ReorderPrivateFields": false,
        "ExtractMagicNumbers": false,
        "SimplifyBooleanExpressions": false,
        "RemoveRedundantCode": false,
        "SimplifyStringOperations": false,
        "UseExpressionBodiedMembers": false,
        "MaxOperationsPerRun": 1
      }
    }
  }
}
```

Note: `SolutionPath` and `IgnoreProjects` are configured at the parent Workload level. By default, all projects in the solution are processed. See [Configuration Guide](configuration.md) for details.
