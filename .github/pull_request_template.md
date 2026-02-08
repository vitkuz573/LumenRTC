## Summary

- What changed
- Why it changed
- Risk level

## ABI Checklist (required for ABI-related PRs)

- [ ] `scripts/abi.sh check-all --skip-binary`
- [ ] `scripts/abi.sh generate --skip-binary --check --fail-on-sync`
- [ ] `scripts/abi.sh codegen --skip-binary --check --fail-on-sync`
- [ ] `dotnet build src/LumenRTC/LumenRTC.csproj` (if C# interop is in scope)
- [ ] If ABI changed intentionally: baseline/changelog updated and reviewed
- [ ] Policy waivers (if any) include owner + reason + `expires_utc`
- [ ] Added/updated tests for parser/policy/diff behavior

## Validation Notes

Paste key command outputs or artifact links.

## Release Impact

- ABI classification: `none | additive | breaking`
- Required version bump: `none | minor | major`
