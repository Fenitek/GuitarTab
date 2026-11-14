// alphaTab inizializza pigramente metadati SMuFL globali senza sincronizzazione.
// Le classi di test non devono costruire i primi Settings in parallelo.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
