using Xunit.Sdk;
using Xunit.v3;

// WebApplicationFactory<Program> hosts share static startup state, so a host that fails on purpose
// (the zero-limit tests) breaks other hosts starting at the same time. Run test classes one at a time.
[assembly: Parallelization(Mode = ParallelMode.None)]
