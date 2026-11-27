# Agent Instructions

This project uses **bd** (beads) for issue tracking. Run `bd prime` for full workflow context.

> **Single source of truth (2026-11-24):** `docs/PLAN.md` and `docs/PROGRESS.md` were retired and deleted.
> All plans, progress, remaining work and lessons live in **bd** only — `bd ready`, `bd list`, `bd stats`,
> `bd remember` / `bd memories`. Their full text before deletion is archived as closed issues
> `FHeroes2Sharp-3so` (PLAN) and `FHeroes2Sharp-jwk` (PROGRESS); any `§x` citation in code or docs refers to
> those issues. Do not recreate plan/progress markdown files.

> **Architecture in one line:** Issues live in a local Dolt database
> (`.beads/dolt/`); cross-machine sync uses `bd dolt push/pull` (a
> git-compatible protocol), stored under `refs/dolt/data` on your git
> remote — separate from `refs/heads/*` where your code lives.
> `.beads/issues.jsonl` is a passive export, not the wire protocol.
>
> See [sync-concepts](https://github.com/gastownhall/beads/blob/main/docs/core-concepts/sync-concepts.md)
> for the one-screen overview and anti-patterns (don't treat JSONL as the
> source of truth; don't `bd import` during normal operation; don't
> reach for third-party Dolt hosting before trying the default).

## Quick Reference

```bash
bd ready              # Find available work
bd show <id>          # View issue details
bd update <id> --claim  # Claim work atomically
bd close <id>         # Complete work
bd dolt push          # Push beads data to remote
```

## Non-Interactive Shell Commands

**ALWAYS use non-interactive flags** with file operations to avoid hanging on confirmation prompts.

Shell commands like `cp`, `mv`, and `rm` may be aliased to include `-i` (interactive) mode on some systems, causing the agent to hang indefinitely waiting for y/n input.

**Use these forms instead:**
```bash
# Force overwrite without prompting
cp -f source dest           # NOT: cp source dest
mv -f source dest           # NOT: mv source dest
rm -f file                  # NOT: rm file

# For recursive operations
rm -rf directory            # NOT: rm -r directory
cp -rf source dest          # NOT: cp -r source dest
```

**Other commands that may prompt:**
- `scp` - use `-o BatchMode=yes` for non-interactive
- `ssh` - use `-o BatchMode=yes` to fail instead of prompting
- `apt-get` - use `-y` flag
- `brew` - use `HOMEBREW_NO_AUTO_UPDATE=1` env var

<!-- BEGIN BEADS INTEGRATION v:1 profile:minimal hash:46cd31e7 -->
## Beads Issue Tracker

This project uses **bd (beads)** for issue tracking. Run `bd prime` to see full workflow context and commands.

### Quick Reference

```bash
bd ready              # Find available work
bd show <id>          # View issue details
bd update <id> --claim  # Claim work
bd close <id>         # Complete work
```

### Rules

- Use `bd` for ALL task tracking — do NOT use TodoWrite, TaskCreate, or markdown TODO lists
- Run `bd prime` for detailed command reference and session close protocol
- Use `bd remember` for persistent knowledge — do NOT use MEMORY.md files

**Architecture in one line:** issues live in a local Dolt DB; sync uses `refs/dolt/data` on your git remote; `.beads/issues.jsonl` is a passive export. See https://github.com/gastownhall/beads/blob/main/docs/core-concepts/sync-concepts.md for details and anti-patterns.

## Agent Context Profiles

The managed Beads block is task-tracking guidance, not permission to override repository, user, or orchestrator instructions.

- **Conservative (default)**: Use `bd` for task tracking. Do not run git commits, git pushes, or Dolt remote sync unless explicitly asked. At handoff, report changed files, validation, and suggested next commands.
- **Minimal**: Keep tool instruction files as pointers to `bd prime`; use the same conservative git policy unless active instructions say otherwise.
- **Team-maintainer**: Only when the repository explicitly opts in, agents may close beads, run quality gates, commit, and push as part of session close. A current "do not commit" or "do not push" instruction still wins.

## Session Completion

This protocol applies when ending a Beads implementation workflow. It is subordinate to explicit user, repository, and orchestrator instructions.

1. **File issues for remaining work** - Create beads for anything that needs follow-up
2. **Run quality gates** (if code changed) - Tests, linters, builds
3. **Update issue status** - Close finished work, update in-progress items
4. **Handle git/sync by active profile**:
   ```bash
   # Conservative/minimal/default: report status and proposed commands; wait for approval.
   git status

   # Team-maintainer opt-in only, unless current instructions forbid it:
   git pull --rebase
   bd dolt push
   git push
   git status
   ```
5. **Hand off** - Summarize changes, validation, issue status, and any blocked sync/commit/push step

**Critical rules:**
- Explicit user or orchestrator instructions override this Beads block.
- Do not commit or push without clear authority from the active profile or the current user request.
- If a required sync or push is blocked, stop and report the exact command and error.
<!-- END BEADS INTEGRATION -->

<!-- BEGIN BEADS CODEX SETUP: generated by bd setup codex -->
## Beads Issue Tracker

Use Beads (`bd`) for durable task tracking in repositories that include it. Use the `beads` skill at `.agents/skills/beads/SKILL.md` (project install) or `~/.agents/skills/beads/SKILL.md` (global install) for Beads workflow guidance, then use the `bd` CLI for issue operations.

### Quick Reference

```bash
bd ready                # Find available work
bd show <id>            # View issue details
bd update <id> --claim  # Claim work
bd close <id>           # Complete work
bd prime                # Refresh Beads context
```

### Rules

- Use `bd` for all task tracking; do not create markdown TODO lists.
- Run `bd prime` when Beads context is missing or stale. Codex 0.129.0+ can load Beads context automatically through native hooks; use `/hooks` to inspect or toggle them.
- Keep persistent project memory in Beads via `bd remember`; do not create ad hoc memory files.
- 在执行任何 `feature` 或 `task` 类型的 Issue 时，如果发现以下任一情况，**严禁在单个 Issue 中硬编码完成**，必须立即执行拆分：
1. **上下文消耗预警**：单个任务无法在一次干净的会话（Context Window）内完成并验证测试。
2. **发现隐藏依赖**：实现过程中发现了未预料到的前置重构或独立子需求。

### 拆分执行步骤：
1. **升级为 Epic**：将当前过大的 Issue 类型改为 `epic`：
   `bd update <current-id> --type epic`
2. **创建子 Issue**：按独立可测试的最小粒度创建子任务，并绑定父级关系：
   `bd create --parent <current-id> --type task --title "子任务1：数据库Schema迁移"`
   `bd create --parent <current-id> --type task --title "子任务2：实现核心接口"`
3. **建立执行顺序依赖**（如果子任务有先后顺序）：
   `bd dep add <child-2-id> <child-1-id>` （表示 child-2 依赖 child-1 完成）
4. **重新获取可执行任务**：运行 `bd ready` 领取第一个无阻塞的子任务继续开发，不要直接停留在原 Epic 上。

## 快捷执行指令 (Quick Resume Trigger)

当用户的输入仅为“**继续**”、“**go**”、“**continue**”或“**next**”（不带其他具体指令）时，立即按以下标准流程自动拉取并执行 Beads 任务，无需向用户二次确认：

1. **检查当前状态**：运行 `bd list --status in_progress` 查看是否有正在进行中（未关闭）的 Issue。
   - 如果已有 `in_progress` 的 Issue，直接读取其详情（`bd show <id>`）并从断点处继续开发。
2. **拉取新任务**：如果没有进行中的任务，运行 `bd ready` 获取当前无阻塞、最高优先级的就绪 Issue。
   - 如果 `bd ready` 返回空，运行 `bd list --status open` 向用户汇报剩余未完成但被阻塞的 Issue 列表及原因，并停止执行。
3. **认领并开工**：
   - 认领排名第一的就绪 Issue：`bd update <id> --claim`
   - 读取完整需求描述：`bd show <id>`
   - **评估体量**：先检查该任务是否过大（参考《自动拆分规则》）；若过大则先拆分为子任务并重新运行 `bd ready`，否则直接开始编写代码与测试。
4. **单任务闭环**：完成代码修改与验证后，更新状态并关闭当前 Issue（`bd close <id>`），然后简要汇报完成情况并等待下一条指令（不要擅自连续开启下一个 Issue，除非用户再次说“继续”）。


**Architecture in one line:** issues live in a local Dolt DB; sync uses `refs/dolt/data` on your git remote; `.beads/issues.jsonl` is a passive export. See https://github.com/gastownhall/beads/blob/main/docs/core-concepts/sync-concepts.md for details and anti-patterns.
<!-- END BEADS CODEX SETUP -->
