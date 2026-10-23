// WebApplicationFactory<Program> hosts share static startup state, so a host that fails on purpose
// (the zero-limit tests) breaks other hosts starting at the same time. Run test classes one at a time.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
