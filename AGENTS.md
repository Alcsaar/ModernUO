Read and follow all instructions in CLAUDE.md in this repository's root.
# ModernUO Development Instructions

## General
- This project uses ModernUO.
- All code must be ModernUO compatible.
- Output full drop-in ready .cs files unless specifically asked otherwise.
- Do not assume APIs exist.
- Ask before introducing era-specific mechanics.
- Always add code blocks around injected/changed code with a description of its purpose - ESPECIALLY when editing core files

## File Locations
- Custom systems go under:
  Projects/UOContent/Custom/

- Config persistence goes under:
  Distribution/Configuration/<SystemName>/
  Note that we should avoid using json output for systems that log a lot of data

## Serialization
- Use [SerializationGenerator] partial classes.
- Do not serialize TimerExecutionToken.
- Restore timers in [AfterDeserialization].

## Gumps
- Correct response signature:
  public override void OnResponse(NetState sender, in RelayInfo info)

- Prefer DynamicGump unless layout is truly static.
- Avoid empty gumps.

## Commands
- Register commands in Configure().
- Include test/debug commands when useful.

## Content Rules
- Use GenerateLoot instead of OnDeath.
- Use Constructible constructors where appropriate.
- Include cleanup in OnDelete/OnAfterDelete.

## Performance
- Avoid unnecessary LINQ in hot paths.
- Avoid World.Items or World.Mobiles iteration.
- Use pooled collections where appropriate.

## Feature Flags
- New toggleable systems should integrate with FeatureFlagManager when reasonable.

## Git Workflow
- Start new work from:
  git checkout develop
  git pull
  git checkout -b feature/<feature-name>

- Keep commits focused and modular.
- Make suggestions on when we should save a branch / create a sub branch / etc

