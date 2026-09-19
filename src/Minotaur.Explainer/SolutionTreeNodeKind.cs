namespace Minotaur.Explanation;

public enum SolutionTreeNodeKind
{
	/*
	 * Not determined yet.
	 */
	Invalid,

	/*
	 * Any other node. This should NEVER appear in an optimized tree with a single correct path.
	 * This may include (but does not have to!):
	 * - nodes with > 1 correct continuations
	 * - nodes with > 2 incorrect continuations
	 * - nodes with >= 2 incorrect continuations but a correct parent
	 * - nodes with > 2 continuations (any number of correct/incorrect)
	 * 
	 * TODO die namen/arten sind nicht 100% richtig für solche baeume.
	 */
	Other,

	/*
	 * The root node
	 */
	Start,

	/*
	 * Single continuation on the correct path.
	 * There are no other options, neither correct nor wrong.
	 */
	Single_Correct,

	/*
	 * Single continuation on the wrong path.
	 * There are no other options, neither correct nor wrong.
	 */
	Single_Wrong,

	/*
	 * Decision node between >= 2 options, one correct and one incorrect.
	 * This is the sole correct option.
	 */
	Decision_Single_Correct,

	/*
	 * Decision node between exactly 2 options, one correct and one incorrect.
	 * This is the wrong option.
	 */
	Decision_Single_Wrong,

	/*
	 * Decision node between >= 2 options.
	 * All are incorrect.
	 */
	Decision_All_Wrong,
}
