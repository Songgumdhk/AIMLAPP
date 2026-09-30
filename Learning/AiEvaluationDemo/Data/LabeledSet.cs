using AIMLAPP.Learning.AiEvaluationDemo.Models;

namespace AIMLAPP.Learning.AiEvaluationDemo.Data;

// A frozen corpus and a frozen question set. Evaluation only means something
// when both stay still while you change the retriever or the prompt.
//
// Document order matters for the naive baseline: the first 3 are distractors,
// so "return the first 3 files" never lands on a gold document.
//
// Edit this file only on purpose. Scores belong to this exact corpus, and FixtureCheck
// throws at startup if an edit breaks a fact the chapter relies on. See §2-3.
public static class LabeledSet
{
    public static readonly IReadOnlyList<EvalDoc> Documents =
    [
        new("hr-onboarding", "First-Year Onboarding Checklist",
            "During the first year after joining, new hires complete a 3-day orientation, collect a laptop, and finish security training in week one. This checklist covers setup tasks. It does not describe paid time off."),

        new("eng-oncall", "On-Call Rotation",
            "The primary on-call engineer must acknowledge a page within 15 minutes. Handoff is every Monday at 09:00. This rotation is the engineering pager, not a security hotline."),

        new("office-snacks", "Office Snack Menu",
            "The kitchen stocks coffee, tea, and fruit on Monday mornings. Snacks are for people in the building during work hours."),

        new("hr-vacation", "Vacation Policy",
            "Full-time employees accrue 15 days of paid vacation per year during their first two years, then 20 days per year. Up to 10 unused days can carry into the next year. Request time off in Workday at least two weeks ahead."),

        new("hr-parental", "Parental Leave",
            "Full-time employees get 12 weeks of paid parental leave after the birth or adoption of a child. The leave must be used within 12 months of the event. Notify the manager 30 days ahead."),

        new("hr-remote", "Remote Work",
            "Employees may work from home up to 2 days per week. The other 3 days are in the office. Core hours are 10:00 to 15:00 local time."),

        new("it-password", "Password Reset",
            "If you cannot sign in, open https://login.acme.com and choose Forgot Password. The reset link expires after 30 minutes. Call the IT helpdesk at extension 4200 if the link never arrives."),

        new("sec-incident", "Security Incident Response",
            "Report a suspected breach, phishing email, or data leak to the Security Operations Center at extension 9911 or soc@acme.com. Do not investigate it yourself. Critical incidents are acknowledged within 15 minutes."),

        new("fin-expense", "Expense Policy EXP-75",
            "Policy EXP-75: meals are reimbursed up to $75 per person. A receipt is required for any meal over $25. Alcohol is not reimbursable. Submit the report within 30 days."),
    ];

    // Each case: id, question, GOLD DOCUMENT IDS, and a note on why it is in the set.
    // Gold ids are the answer key for retrieval: which documents should come back, not
    // which sentence the model should write. Each question is built so one metric or one
    // retriever weakness has something to show (exact tokens, paraphrases, two golds).
    public static readonly IReadOnlyList<EvalCase> Cases =
    [
        new("vacation-paraphrase",
            "How many paid days off does a person get in their first year after joining?",
            ["hr-vacation"],
            "The onboarding checklist shares first, year, joining, paid, and off. The gold document says vacation, which this question never uses."),

        new("expense-code",
            "What does policy EXP-75 cover?",
            ["fin-expense"],
            "EXP-75 appears in one document. Keyword ranking is built for a token like that."),

        new("parental-paraphrase",
            "How long can someone stay home with pay after a baby is born?",
            ["hr-parental"],
            "The question says baby, born, and pay. The document says child, birth, and paid. There is no shared keyword."),

        new("hotline-number",
            "Who answers extension 9911?",
            ["sec-incident"],
            "9911 appears in one document, so keyword search has an exact token to rank first."),

        new("time-off-and-remote",
            "What are the rules for vacation time and for working from home?",
            ["hr-vacation", "hr-remote"],
            "Two gold documents. Recall@3 is 1 only when both are inside the top 3."),

        new("locked-out",
            "I am locked out of my account. How do I get back in?",
            ["it-password"],
            "The question never says password, reset, or sign in. The procedure document never says locked or account."),
    ];

    public static EvalDoc Doc(string id) =>
        Documents.First(d => d.Id == id);
}

// Fixed answers for the judge demos. They do not depend on retrieval quality,
// so a bad search cannot hide what the judge is doing.
// Each pair holds one variable fixed so the judge's reaction to it is visible:
//   Faithful vs Invented vacation - same true first sentence, then invented claims (§6).
//   FaithfulExpense vs FluentLie  - both on topic, only one faithful (§7 pointwise).
//   Brief vs Detailed             - both faithful, only length differs (§7 pairwise).
public static class JudgeFixtures
{
    public static string VacationContext => LabeledSet.Doc("hr-vacation").Content;

    public const string FaithfulVacation =
        "During their first two years, full-time employees accrue 15 days of paid vacation per year. Up to 10 unused days can carry into the next year.";

    public const string InventedVacation =
        "During their first two years, full-time employees accrue 15 days of paid vacation per year. Unused vacation is paid out in cash every December, and contractors receive the same 15 days.";

    public static string ExpenseContext => LabeledSet.Doc("fin-expense").Content;

    public const string ExpenseQuestion = "What is the meal reimbursement rule?";

    public const string FaithfulExpense =
        "Meals are reimbursed up to $75 per person. A receipt is required above $25. Alcohol is not reimbursable.";

    public const string FluentLie =
        "Meals are reimbursed up to $150 per person, receipts are optional, and alcohol is covered for client dinners.";

    public const string BriefExpense =
        "The meal cap is $75 per person, and receipts are required over $25.";

    public const string DetailedExpense =
        "Under policy EXP-75, meals are reimbursed up to $75 per person. Keep a receipt when the meal is over $25. Alcohol is not reimbursable.";

    // Not answerable from any document. Strict should refuse; Ungrounded should invent.
    public const string MissingFactQuestion = "What is the CEO's favorite restaurant for team dinners?";
}
