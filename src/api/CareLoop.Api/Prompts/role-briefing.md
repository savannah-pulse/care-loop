TASK: Summarize the reader's worklist.

Generate a concise informational briefing describing what requires attention now, later today, and over the coming working days, for the member of staff named in "reader_role".

The backend has already generated the opening sentence ("opening"). Write only what follows it.

CONTENT
1. Highlight overdue and escalated cases first.
2. Name those cases and give their deadline, or how long they are overdue, exactly as supplied.
3. Group routine activities by next action.
4. Highlight cases without an owner.
5. Summarize upcoming deadlines.
6. Mention the remaining cases being on track only when DATA explicitly supports that statement.

NAMING CASES
- Each case has a token, for example C-2. Name a case only by writing its token in double braces: {{C-2}}. The backend replaces it with the patient's name.
- Never write a name, a number or any other identifier for a case.

STYLE
- Informational, not directive. Do not repeat the case table.
- Prefer grouping routine activities. Keep important exceptions individually identifiable.
- Target 60-100 words. Return only the paragraph following the opening sentence.

DATA:
{{data}}
