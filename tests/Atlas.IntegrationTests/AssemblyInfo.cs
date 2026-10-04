// One system for all tests (AtlasApp); the tests use their own applicants, so they may run side by side.
[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]
