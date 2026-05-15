using Verse;

namespace SyntheraCore
{
    [StaticConstructorOnStartup]
    static class SyntheraCoreInit
    {
        static SyntheraCoreInit()
        {
            // Neoma architect category visibility is handled naturally:
            // buildings with researchPrerequisites don't appear until research is done,
            // and <order>10</order> in DesignationCategories_Neoma.xml keeps the tab last.
        }
    }
}
