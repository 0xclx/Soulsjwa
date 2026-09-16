/**
 * Variable id the JsonLogic fail-rule builder uses for the shared
 * "how many OTHER event competitors have already completed this objective"
 * count. Must match the server's `RuleEvaluator.CompetitorCompletionsVariable`
 * constant exactly, since it's the JsonLogic `var` name merged into the
 * evaluation data at fail-rule evaluation time — see
 * `docs/flows/objective-completion.md`.
 */
export const COMPETITOR_COMPLETIONS_VAR_ID = 'competitorCompletions'

/**
 * Key used to register the "Predefined Objectives" toolbox category as a
 * dynamic/custom category (see predefinedObjectivesCategory.ts), so its
 * flyout contents can be re-filtered as the user types into the search box
 * without reinjecting the whole workspace.
 */
export const PREDEFINED_OBJECTIVES_CATEGORY_KEY = 'PREDEFINED_OBJECTIVES_SEARCH'
