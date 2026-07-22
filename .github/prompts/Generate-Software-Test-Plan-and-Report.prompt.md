# Prompt-02-Generate-Software-Test-Plan-and-Report.md

# CISC 593/594  
# Prompt 02 – Generate and Maintain a Living Software Test Plan and Report

## Objective

Create or update the Software Test Plan and Report for your project.

This is a **living verification document** maintained throughout the semester. It is not a one-time report written after implementation is complete. Update it whenever requirements change, features are added, defects are discovered, tests are created, regressions are prevented, or the CI/CD pipeline changes.

Maintain the document at:

```text
/docs/Software_Test_Plan_and_Report.md
```

The document must remain synchronized with the Product Requirements Document (PRD), source code, automated tests, Git history, and current software release.

---

# Instructions to the AI Assistant

Review the current GitHub repository, including:

- `/docs/Product_Requirements_Document.md`
- the current Software Test Plan and Report, if one already exists
- source-code folders
- test folders
- configuration files
- dependency files
- CI/CD workflow files
- commit history, branches, tags, and releases when accessible
- defect or issue records when accessible

Create the document if it does not exist.

If the document already exists:

- do not rewrite it from scratch
- preserve valid prior content and execution history
- update only the sections affected by repository changes
- retain previously reported defects and regression tests
- update the revision history
- clearly mark obsolete information instead of silently removing important evidence

Do not invent tests, requirements, results, tools, environments, defects, CI runs, coverage percentages, or execution evidence.

Distinguish clearly among:

- **Planned** tests
- **Implemented but not executed** tests
- **Executed** tests
- **Passed** tests
- **Failed** tests
- **Blocked** tests
- **Deferred** tests
- **Not applicable** tests

When information cannot be established from the repository, write:

> **To Be Completed**

When a test has not been executed, do not report it as passed.

Use the exact requirement identifiers from the PRD. Every test case must trace to at least one approved requirement.

---

# Required Output

Generate or update:

```text
/docs/Software_Test_Plan_and_Report.md
```

Use the following structure.

---

# Cover Page and Document Metadata

Include:

- Project name
- Student name or team members
- Course
- Semester
- Repository URL
- Current branch
- Current commit SHA
- Current release or tag
- Document version
- Document status
- Last updated date
- Test period covered
- Primary test framework(s)
- CI/CD workflow status, when verified

Use `To Be Completed` for unavailable values.

---

# Document Revision History

Create and maintain this table:

| Document Version | Date | Git Commit | Sections Updated | Change Description | Author/Reviewer |
|---|---|---|---|---|---|

Add a row whenever the document changes materially.

Do not delete prior revision records.

---

# Table of Contents

Generate a GitHub-friendly table of contents.

---

# 1. Purpose and Scope

## 1.1 Purpose

Explain what this document plans, records, and verifies.

## 1.2 Software Under Test

Identify:

- system or product name
- release/version
- branch
- commit SHA
- major components
- major Level-2 capabilities covered
- deployment form
- known external dependencies

## 1.3 Test Scope

Identify what is included in the current verification effort.

## 1.4 Out-of-Scope Items

List excluded items and explain why they are excluded.

## 1.5 Verification Objectives

State the outcomes that testing must establish, including:

- functional correctness
- quality requirement satisfaction
- performance requirement satisfaction
- risk mitigation effectiveness
- regression prevention
- reproducibility
- release readiness

---

# 2. Verification Basis

Identify the authoritative sources used to derive tests:

- Product Requirements Document
- Level-2 capabilities
- functional requirements
- quality requirements
- performance requirements
- undesirable events
- risk statements and scores
- risk mitigations
- architecture or design documentation
- user stories or acceptance criteria
- defect history
- source code and interfaces

Create this table:

| Verification Basis ID | Source Artifact | Version/Commit | Purpose |
|---|---|---|---|

---

# 3. Test Environment

Document the actual environment used for testing.

## 3.1 Hardware Environment

Include applicable details such as:

- processor
- memory
- storage
- dedicated devices
- processor boards
- sensors
- mobile devices
- virtual machines
- cloud services

## 3.2 Software Environment

Include exact versions when known:

- operating system
- programming language
- runtime
- framework
- database
- browser
- test frameworks
- coverage tools
- build tools
- linters
- static-analysis tools
- containers
- external services and APIs

Use this table:

| Item | Version | Purpose | Configuration Source |
|---|---|---|---|

## 3.3 Test Environment Setup

Provide reproducible, numbered setup instructions.

Include:

- repository clone command
- target branch or tag
- dependency installation
- environment variables
- secrets handling
- database initialization
- migration commands
- seed-data commands
- build commands
- application startup
- test execution commands
- environment reset and cleanup

Do not expose secret values.

## 3.4 Test Data and Fixtures

Document:

- test accounts
- synthetic data
- map or simulation fixtures
- sample files
- database seeds
- mock responses
- API stubs
- invalid data
- boundary values
- randomized data
- data reset procedures

Use this table:

| Test Data ID | Description | Source/Location | Used By | Reset Procedure |
|---|---|---|---|---|

---

# 4. Test Strategy

Explain how testing will provide sufficient verification.

## 4.1 Risk-Based Test Prioritization

Use the PRD risk analysis to prioritize testing.

High-risk requirements and mitigations must receive greater test depth.

Create this table:

| Priority | UE ID | Risk ID | Risk Score | Mitigation | Related Requirement IDs | Planned Verification |
|---|---|---|---:|---|---|---|

Explain how risk scores influenced test order, depth, and evidence requirements.

## 4.2 Requirements-Based Testing

Explain how tests are derived from functional, quality, and performance requirements.

## 4.3 Positive Testing

Verify valid inputs and expected workflows.

## 4.4 Negative Testing

Verify prevention, rejection, error messages, safe failure, and recovery.

## 4.5 Boundary Value Analysis

Identify numeric, size, time, capacity, and count boundaries.

## 4.6 Equivalence Class Partitioning

Identify valid and invalid input classes when applicable.

## 4.7 State-Transition and Workflow Testing

Verify behavior across states, sequences, phases, roles, and lifecycle transitions.

## 4.8 Regression Testing

Explain how existing functionality is reverified after changes.

## 4.9 Test Independence and Repeatability

Explain how tests avoid hidden dependencies, shared-state interference, and order dependence.

---

# 5. Testing Levels

## 5.1 Unit Testing

Describe:

- units or functions tested
- isolation strategy
- mocks, stubs, fakes, or spies
- expected code-level behaviors
- test framework and commands
- current execution status

Use this summary table:

| Unit Test ID/Group | Component | Requirement IDs | Test File | Status | Evidence |
|---|---|---|---|---|---|

Unit tests may support verification, but they do not replace system-level test procedures.

## 5.2 Integration Testing

Identify interactions among:

- classes or modules
- services
- databases
- files
- queues
- APIs
- external services
- hardware interfaces

Use this table:

| Integration Test ID | Components/Interfaces | Requirement IDs | Expected Interaction | Status | Evidence |
|---|---|---|---|---|---|

## 5.3 System Testing

Verify complete end-to-end behavior from an external or user-visible perspective.

System tests must cover:

- normal workflows
- invalid or prohibited workflows
- adverse conditions
- data persistence
- roles and permissions
- external dependency failures
- recovery behavior
- risk mitigations
- quality requirements
- performance requirements where applicable

## 5.4 Acceptance Testing

When applicable, identify tests that demonstrate stakeholder or release acceptance.

## 5.5 Regression Testing

Maintain a regression suite containing:

- critical user workflows
- high-risk functionality
- prior defects
- backward compatibility
- earlier release capabilities

---

# 6. Verification of Nondeterministic and Variable Behavior

Complete this section when the software includes:

- randomness
- simulation
- AI-generated output
- time-sensitive behavior
- concurrency
- asynchronous processing
- external APIs
- generated data
- probabilistic algorithms

## 6.1 Sources of Nondeterminism

Use this table:

| Source ID | Component | Source of Variability | Why It Exists | Verification Risk |
|---|---|---|---|---|

## 6.2 Reproducibility Controls

Document applicable controls:

- fixed random seeds
- dependency injection
- mocked random-number generators
- fake clocks
- deterministic fixtures
- recorded API responses
- model version pinning
- temperature or sampling configuration
- bounded retries
- controlled thread schedules
- captured failing seeds

## 6.3 Property-Based or Invariant Testing

Define properties that must remain true even when exact outputs vary.

Examples include:

- generated paths remain within boundaries
- moves are legal
- blocked locations are avoided
- totals remain internally consistent
- unauthorized actions never succeed
- required citations are present
- responses do not exceed configured limits
- conservation or domain rules remain true

Use this table:

| Property Test ID | Requirement IDs | Property/Invariant | Input Generation Method | Runs | Acceptance Criterion | Result |
|---|---|---|---|---:|---|---|

## 6.4 Statistical Testing

Use statistical testing only when appropriate.

Document:

- number of runs
- seed range
- population or sample
- measured variables
- expected distribution or threshold
- confidence or tolerance
- rationale for acceptance criteria

Use this table:

| Statistical Test ID | Requirement IDs | Runs | Metric | Acceptance Threshold | Actual Result | Status |
|---|---|---:|---|---|---|---|

Do not choose a threshold merely because the current implementation passes it. Tie the threshold to a requirement, scientific rationale, stakeholder expectation, or documented design decision.

## 6.5 Failure Reproduction

For every nondeterministic failure, record:

- seed
- input data
- environment
- branch
- commit SHA
- test command
- observed outcome

---

# 7. Detailed Test Case Specifications

Every detailed test case must include:

- Test Case ID
- Title
- Test Level
- Requirement ID(s)
- Level-2 Capability ID
- Related UE ID and Risk ID when applicable
- Risk score when applicable
- Priority
- Test objective
- Preconditions
- Test data
- environment/configuration
- detailed steps
- expected result for each step
- actual result for each step after execution
- final status
- evidence link
- defect ID when failed
- execution date
- tester
- branch and commit SHA

Use this standard test-case format:

## TC-[Capability].[Sequence] — Test Title

| Field | Value |
|---|---|
| Test Case ID | |
| Test Level | Unit / Integration / System / Acceptance / Performance / Statistical / Property-Based |
| Level-2 Capability | |
| Requirement ID(s) | |
| Related UE/Risk | |
| Risk Score/Priority | |
| Objective | |
| Preconditions | |
| Test Data | |
| Environment | |
| Branch/Commit | |
| Execution Status | Planned / Not Run / Passed / Failed / Blocked / Deferred |

### Test Procedure

| Step | Action | Expected Result | Actual Result | Step Status | Evidence |
|---:|---|---|---|---|---|
| 1 | | | | | |
| 2 | | | | | |

### Test Case Conclusion

- Final Status:
- Defect ID:
- Notes:
- Execution Date:
- Tester:

Generate enough test cases to verify all approved requirements. Do not create duplicate tests merely to increase test counts.

---

# 8. Quality Requirement Verification

Create a separate subsection for each applicable quality attribute.

Examples:

- reliability
- availability
- security
- usability
- maintainability
- scalability
- portability
- interoperability
- testability
- safety
- AI explainability
- AI grounding
- AI fairness, where applicable

Use this table:

| Quality Requirement ID | Quality Attribute | Verification Method | Measurement | Acceptance Criterion | Result | Status |
|---|---|---|---|---|---|---|

Qualitative statements such as "the system is user-friendly" are insufficient unless supported by defined evidence and acceptance criteria.

---

# 9. Performance Testing

Trace every performance test to an approved performance requirement.

Potential measures include:

- response time
- latency
- throughput
- simulation tick duration
- frame/update rate
- memory usage
- CPU utilization
- database query time
- concurrent users
- queue depth
- file-processing time
- AI inference latency
- pathfinding time
- startup time

Use this table:

| Performance Test ID | Requirement ID | Workload | Environment | Metric | Acceptance Threshold | Actual Result | Status |
|---|---|---|---|---|---|---|---|

Document warm-up, repetitions, sample size, and measurement method.

Do not report performance results without identifying the environment and workload.

---

# 10. CI/CD Verification

## 10.1 Workflow Configuration

Document:

- workflow file path
- trigger branches
- pull-request triggers
- manual triggers
- runtime versions
- dependency installation
- test commands
- linting/static analysis
- coverage generation
- artifact retention
- build/package steps
- deployment steps, when applicable

Use this table:

| CI/CD Control | Configuration | File/Location | Current Status | Evidence |
|---|---|---|---|---|

## 10.2 CI Execution Evidence

Record verified pipeline runs:

| Run Date | Branch/PR | Commit SHA | Workflow | Tests Run | Result | Evidence Link |
|---|---|---|---|---:|---|---|

## 10.3 CI/CD Limitations

Identify any tests not suitable for CI and explain how they are otherwise verified.

A CI configuration file does not count as functioning CI unless the workflow is correctly located, triggered, executed, and supported by pass/fail tests.

---

# 11. Test Execution Summary

Maintain a current summary.

| Test Level | Planned | Implemented | Executed | Passed | Failed | Blocked | Deferred |
|---|---:|---:|---:|---:|---:|---:|---:|
| Unit | | | | | | | |
| Integration | | | | | | | |
| System | | | | | | | |
| Acceptance | | | | | | | |
| Performance | | | | | | | |
| Property-Based | | | | | | | |
| Statistical | | | | | | | |
| Regression | | | | | | | |

Also include:

- overall pass rate
- unresolved critical defects
- unresolved high-risk requirements
- release recommendation
- test completion date
- tested commit SHA

Do not calculate a pass rate using unexecuted tests as passed.

---

# 12. Test Execution Evidence

Identify evidence locations such as:

- repository test files
- CI logs
- coverage reports
- screenshots
- terminal output
- database queries
- API responses
- generated reports
- simulation CSV files
- performance logs
- screen recordings
- issue links

Use repository-relative links whenever possible.

Use this table:

| Evidence ID | Test Case ID(s) | Evidence Type | Repository Location/Link | Commit SHA |
|---|---|---|---|---|

Evidence should support the reported actual result. Source-code inspection alone should not normally be reported as successful execution of a system test.

---

# 13. Defect Log

Record every meaningful test failure.

| Defect ID | Date Found | Test Case ID | Requirement ID | Description | Severity | Priority | Status | Root Cause | Fix Commit | Regression Test |
|---|---|---|---|---|---|---|---|---|---|---|

Recommended severity definitions:

- **Critical:** system unusable, severe data loss, unsafe outcome, or core purpose defeated
- **High:** major capability unavailable with no acceptable workaround
- **Medium:** capability impaired but workaround exists
- **Low:** limited impact, cosmetic issue, or minor documentation problem

Do not remove closed defects. Preserve them as engineering history.

---

# 14. Regression Test Log

Every corrected defect should normally produce a regression test.

| Regression ID | Defect ID | Requirement ID | Test Case ID | Failure Prevented | Added in Commit | Latest Result |
|---|---|---|---|---|---|---|

Also record regression testing after:

- new feature implementation
- refactoring
- dependency upgrades
- database changes
- API changes
- architecture changes
- configuration changes

---

# 15. Requirements-to-Test Traceability Matrix

Every approved functional, quality, and performance requirement must appear.

| Requirement ID | Level-2 Capability | Requirement Summary | Risk/UE | Test Case IDs | Latest Status | Evidence |
|---|---|---|---|---|---|---|

Traceability rules:

1. Every test case must reference at least one requirement.
2. Every approved requirement must have one or more test cases.
3. High-risk requirements should normally have positive, negative, and adverse-condition tests.
4. A requirement with no test is a verification gap.
5. A test with no requirement must be justified as exploratory, regression, compliance, infrastructure, or risk-control testing.

---

# 16. Risk-Mitigation Verification Matrix

Demonstrate whether each mitigation from the PRD is implemented and effective.

| UE ID | Risk ID | Risk Score | Mitigation | Classification | Implementation Evidence | Verification Test IDs | Result |
|---|---|---:|---|---|---|---|---|

Use the same mitigation classifications as the PRD:

- Pure Software
- Hybrid (Software + Hardware)
- Pure Hardware

Do not assume that implementing a mitigation proves it works. Verification evidence is required.

---

# 17. Coverage Analysis

Report applicable forms of coverage:

- requirements coverage
- Level-2 capability coverage
- risk coverage
- mitigation coverage
- workflow/state coverage
- platform/browser coverage
- code coverage
- data/input coverage

Use this table:

| Coverage Type | Covered | Total | Percentage | Method | Known Gap |
|---|---:|---:|---:|---|---|

Code coverage is supplementary. High code coverage does not by itself demonstrate requirements or system coverage.

---

# 18. Testability Assessment

Evaluate how the design enables or obstructs verification.

Use this table:

| Component | Testability Issue | Impact on Testing | Improvement Made/Planned | Related Commit | Benefit |
|---|---|---|---|---|---|

Consider:

- hard-coded dependencies
- direct random-number calls
- hidden state
- global variables
- time dependence
- tightly coupled classes
- large multi-responsibility functions
- inaccessible outputs
- missing interfaces
- unbounded loops
- difficult environment setup
- external services without mocks
- nondeterministic AI output

Explain how testability improvements changed the architecture or testing approach.

---

# 19. Release Readiness Assessment

For the tested release, state:

- tested branch
- tested commit SHA
- release/tag
- total executed tests
- failures
- blocked tests
- unresolved defects
- unmet requirements
- unverified high risks
- CI status
- known limitations
- release recommendation

Use one recommendation:

- Ready for Release
- Ready with Known Limitations
- Not Ready for Release
- Insufficient Evidence

Provide a concise justification based on evidence.

---

# 20. Known Limitations and Verification Gaps

List limitations such as:

- unavailable hardware
- unavailable external services
- untested browsers
- missing load tests
- incomplete security testing
- insufficient simulation runs
- unavailable test data
- unverified AI behavior
- manual-only tests
- flaky tests
- blocked environments

For each gap, state its impact and planned resolution.

---

# 21. Lessons Learned

Document substantive verification lessons.

Examples:

- a requirement was too ambiguous to test
- randomness had to be made controllable
- a component required refactoring for isolation
- a defect exposed a missing adverse-condition requirement
- CI initially executed a script without assertions
- a test revealed an incorrect architectural assumption
- risk analysis changed test priorities

Focus on engineering learning, not a generic summary.

---

# 22. Planned Verification Work

Identify the next verification actions.

| Priority | Planned Work | Related Requirement/Risk | Target Version | Owner | Status |
|---|---|---|---|---|---|

---

# 23. Glossary

Define project-specific testing, domain, simulation, AI, hardware, and quality terms.

---

# Appendices

Include applicable evidence or links.

## Appendix A — Test Commands

List reproducible commands for:

- unit tests
- integration tests
- system tests
- regression tests
- coverage
- linting
- static analysis
- performance tests
- property-based tests
- statistical simulations

## Appendix B — Coverage Reports

Link to or summarize verified reports.

## Appendix C — CI/CD Logs

Link to representative successful and failed runs.

## Appendix D — Screenshots and Execution Artifacts

Use repository-relative links.

## Appendix E — Test Data, Random Seeds, and Simulation Results

Include seeds, fixture identifiers, CSV schemas, and reproduction instructions.

## Appendix F — Deferred Tests

Document tests intentionally deferred and their rationale.

---

# Test Case Identification Standard

Use consistent identifiers.

Recommended patterns:

```text
UT-[Capability]-###
IT-[Capability]-###
ST-[Capability]-###
AT-[Capability]-###
PT-[Capability]-###
PBT-[Capability]-###
STAT-[Capability]-###
REG-[Capability]-###
```

Example:

```text
ST-4.2-003
```

This represents a system test associated with Level-2 Capability 4.2.

A project's existing naming convention may be preserved if it remains unique, consistent, and traceable.

---

# Status Vocabulary

Use only these status values unless the project has an approved alternative:

- Planned
- Implemented
- Not Run
- Passed
- Failed
- Blocked
- Deferred
- Not Applicable

Do not use vague labels such as "Done," "Looks Good," or "Working."

---

# Required Quality Rules

The final document must:

- be professionally written
- use GitHub-compatible Markdown
- preserve revision and execution history
- reflect the actual repository
- trace tests to approved requirements
- trace high-risk tests to undesirable events and risks
- distinguish planned tests from executed tests
- include actual results only when execution evidence exists
- record failed tests honestly
- link defects to fixes and regression tests
- document CI/CD configuration and verified runs
- identify verification gaps and limitations
- update incrementally as the software evolves

Do not:

- invent test execution
- invent coverage percentages
- mark unexecuted tests as passed
- replace system testing with unit-test output alone
- use code inspection as the only evidence of runtime behavior
- delete defect history after fixes
- omit failed or blocked tests from summaries
- generate tests unrelated to requirements merely to increase test counts

The final result should resemble a professional, continuously maintained verification plan and report rather than a one-time academic submission.
