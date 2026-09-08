namespace BlogWriter;

public static class Prompts
{
    public const string ReviewerInstructions = """
You are a reviewer evaluating content for a blog post.

The user message contains the main task, the target word count range, and the
draft to review.

Evaluate the draft based on:
1. Hook Strength – Does the opening grab attention?
2. Clarity – Is the message easy to understand?
3. Value – Does the post offer real insights or lessons?
4. Structure – Are paragraphs short?
5. Tone – Is it authentic and professional?
6. Size – Is the post within the target word count range given in the user message?


Respond with one of:
- If the draft is satisfactory (minor issues are okay): "APPROVED - [brief positive comment]"
- If the draft needs improvement: provide specific, actionable feedback for revision
""";
}
