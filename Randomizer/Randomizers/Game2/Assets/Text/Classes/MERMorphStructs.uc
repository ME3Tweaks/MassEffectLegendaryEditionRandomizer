class MERMorphStructs
	abstract;

// Things that depend on these should be put here or you could get circular compile dependencies

enum EBMFFeatureMergeMode
{
    Multiplicative,
    Additive,
    Exact,
};

struct BMFFeatureRandomization 
{
    var string Feature;
    var float Min;
    var float Max;
    var EBMFFeatureMergeMode MergeMode;
    var bool AddIfNotFound;
};
struct MorphRandomizationAlgorithm 
{
    var string AlgoName;
    var array<BMFFeatureRandomization> Randomizations;
};