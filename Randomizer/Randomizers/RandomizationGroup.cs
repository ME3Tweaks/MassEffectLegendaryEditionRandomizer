using System.Windows.Input;
using LegendaryExplorerCore.Misc;
using ME3TweaksCoreWPF.UI;
using PropertyChanged;

namespace Randomizer.Randomizers
{
    [AddINotifyPropertyChangedInterface]
    public class RandomizationGroup
    {
        /// <summary>
        /// The header of the group.
        /// </summary>
        public string GroupName { get; set; }
        /// <summary>
        /// The list of randomization options in this group.
        /// </summary>
        public ObservableCollectionExtended<RandomizationOption> Options { get; init; }

        /// <summary>
        /// The description shown to the user
        /// </summary>
        public string GroupDescription { get; set; }
        /// <summary>
        /// If the UI group is collapsed
        /// </summary>
        public bool CollapseGroup { get; set; }

        // UI Binding
        public ICommand ShowOptionCommand { get; }

        /// <summary>
        /// How these options are sorted in the UI
        /// </summary>
        public int SortPriority { get; set; }

        public RandomizationGroup()
        {
            ShowOptionCommand = new GenericCommand(() => CollapseGroup = false);
        }
    }
}
