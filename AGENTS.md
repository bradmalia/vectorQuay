# Agent completion rules

- Complete the requested task before ending the turn.
- Do not end immediately after file searches, file reads, or shell commands.
- After exploration, always provide the requested final analysis or implementation.
- For code reviews, produce prioritized findings with file references, risks, and recommended fixes.
- If the review is incomplete, continue reading the necessary files rather than returning an empty response.
- If blocked, state the exact blocker and the next action required.
- Never claim a review is complete unless final findings have been delivered.
