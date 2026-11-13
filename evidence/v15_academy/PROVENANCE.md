# Verification provenance correction

The original `source/head.txt` incorrectly contained
`db7a7dba90258c8e2276bf7ce2b5f9ba92708291`, PR #34's base. The local academy
verification was performed with uncommitted academy changes over that base;
the original logs did not embed the tested source hash. They cannot establish
an exact historical local code revision by themselves.

The committed academy code was independently verified by GitHub Actions run
[43310792796](https://github.com/PhilipGB/patchwork-island/actions/runs/43310792796)
at `a16f616a15853f77a38c4786fffd0aa316a87d52` (Linux build/runtime/exported
graphical gates and Windows build/pure tests both succeeded). The reviewed head
`122654c59160035c5652a8e6d44834150c3b0405` added documentation only and also
passed CI, run
[43315834299](https://github.com/PhilipGB/patchwork-island/actions/runs/43315834299).
These passes did not detect the subsequently reviewed safety defects.

After the fixes, `source/head.txt` and `packaged/head.txt` are written **before**
the fresh verification and export, naming the committed code revision. Source
and packaged logs/summary files and screenshots are replaced by these fresh
runs. The evidence commit is necessarily later than the tested code commit;
it changes evidence/documentation only.

`graphical/walkthrough-*.log` and `performance.log` remain historical. They
predate the review corrections and have no embedded commit attestation; they
are not claimed as corrected-code walkthrough or performance evidence.

The first safety-review refresh tested code `58699c6` and was committed in
evidence/documentation head `1e8bcaf`. The subsequent finite-signal/R-restart
follow-up refresh replaces those automated logs/captures; its tested code is
again named in both `head.txt` files before execution. Historical walkthrough
and performance limitations remain unchanged.

The 2026-11-13 real-input playthrough review refresh replaces the automated
logs/captures again and adds the fresh-save walkthrough log plus all 52 of its
screenshots under `graphical/walkthrough/`. Its tested code is named in both
`head.txt` files before execution (`5a0abda`). The walk-through evidence was
produced by the dev-only playthrough driver using real input; historical
walkthrough and performance limitations remain unchanged.

The merge review of PR #37 then found the guidance left/right labels were
reversed (P2). Code commit `733043d` corrects them and extracts the mapping into
pure `DrivingAcademyRules` helpers. On top of a full gate rerun at that commit
(named in both `head.txt` files before execution), the real-input fresh-save
academy walkthrough was rerun: exit 0, licence on the first TEST01 attempt, and
all 52 screenshots under `graphical/walkthrough/` plus
`graphical/walkthrough-fresh-save.log` were replaced by that run, so the
committed captures show the corrected guidance labels.
