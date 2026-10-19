---
name: log-prompt
description: Append a meaningful prompt and its outcome (accepted / changed / rejected and why) to PROMPTS.md. Use after a planning, coding, debugging or refactoring prompt produced something worth recording, or when the user says "log this prompt".
---
# /log-prompt

PROMPTS.md is a graded deliverable: few, meaningful entries; honest about what was rejected.

1. Pick the section: `## Planning`, `## Coding`, or `## Debugging / refactoring`.
2. Next id = highest `P-NN` + 1.
3. Append:
   ```markdown
   ### P-NN · <short title>
   > "<the prompt, abridged to its essential instruction>"

   - **Accepted:** <what was kept and why it is right>
   - **Changed:** <what was corrected/simplified and why>
   - **Rejected:** <what was thrown away and why> (omit the line only if truly nothing)
   ```
4. Be specific (name the type, constraint, test or bug). No marketing language. Remove placeholder lines like
   "_(entries added …)_" in that section once it has a real entry.
