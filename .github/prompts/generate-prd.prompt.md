# Prompt-01-Generate-PRD.md

# CISC 593/594
# Prompt 01 – Generate and Maintain a Living Product Requirements Document (PRD)

## Objective

Create or update the Product Requirements Document (PRD) for your software project.

This is a **living engineering document** that must evolve throughout the semester. Do NOT rewrite the document from scratch each time. Instead, review the existing document, preserve all valid information, improve it, and update it to reflect the current state of the project.

The document shall be maintained under:

```
/docs/Product_Requirements_Document.md
```

The document shall remain synchronized with your GitHub repository throughout the semester.

---

# Instructions to AI

Review the current version of the Product Requirements Document.

Do NOT regenerate the document from scratch.

Preserve all previous content unless it is obsolete or incorrect.

Update the document to reflect the latest implementation, architecture, requirements, and project direction.

Maintain all previous version history.

Whenever possible, infer information from the current GitHub repository instead of inventing new information.

If information cannot be determined from the repository, clearly identify it as

> **To Be Completed**

rather than making assumptions.

---

# Generate the PRD using the following structure.

---

# Cover Page

Include

- Project Name
- Student(s)
- Course
- Semester
- Repository URL
- Current Branch
- Current Commit SHA
- Current Release Version
- Document Version
- Last Updated

---

# Revision History

Generate a revision history table.

| Version | Date | Git Commit | Description | Author |
|----------|------|------------|-------------|--------|

Update this table every time the document changes.

---

# Table of Contents

Generate automatically.

---

# 1. Product Vision

Describe

- Problem Statement
- Intended Users
- Stakeholders
- Product Goals
- Major Features
- Planned Software Versions

---

# 2. Product Scope

Clearly identify

Included functionality

Excluded functionality

Future enhancements

---

# 3. Software Capabilities

## 3.1 Level-1 Capabilities

Identify approximately seven (±2) major software capabilities following Miller's Law.

Each capability name SHALL begin with an action verb.

Examples

- Manage Users
- Manage Orders
- Generate Reports

Avoid naming capabilities using nouns alone.

---

## 3.2 Level-2 Capabilities

For each Level-1 capability

Identify multiple Level-2 capabilities.

Each Level-2 capability SHALL

- begin with an action verb
- support exactly one Level-1 capability

Number Level-2 capabilities using hierarchical numbering.

Example

```
1. Manage Users

1.1 Register User

1.2 Authenticate User

1.3 Update User Profile
```

Continue numbering consistently throughout the document.

---

# 4. Undesirable Events

For EVERY Level-2 capability

Identify one or more undesirable events.

Each undesirable event shall reference the corresponding Level-2 capability.

Example

| UE ID | Level-2 Capability | Undesirable Event |
|-------|--------------------|-------------------|
| UE-1.1-01 | Register User | Duplicate account created |

---

# 5. Risk Analysis

For EVERY undesirable event

Generate

- Risk Statement
- Likelihood
- Impact
- Risk Score

Use a 5×5 risk matrix.

Likelihood

1 = Rare

2 = Unlikely

3 = Possible

4 = Likely

5 = Almost Certain

Impact

1 = Negligible

2 = Minor

3 = Moderate

4 = Major

5 = Catastrophic

Risk Score

Likelihood × Impact

Range

1–25

Generate a table.

| UE ID | Risk Statement | Likelihood | Impact | Risk Score |

---

# 6. Risk Prioritization

Sort ALL undesirable events by Risk Score.

Highest score first.

Generate

| Priority | UE ID | Risk Score |

This becomes the implementation priority list.

---

# 7. Risk Mitigation

For every identified risk

Identify

- Risk Mitigation Strategy
- Mitigation Classification

Mitigation Classification shall be exactly one of

- Pure Software
- Hybrid (Software + Hardware)
- Pure Hardware

Pure Hardware mitigation should only be used when justified.

For most software projects

Pure Software or Hybrid will be appropriate.

Generate

| UE ID | Risk Mitigation | Classification |

---

# 8. Functional Requirements

Generate functional requirements from the Level-2 capabilities.

Every functional requirement SHALL

- trace back to one Level-2 capability
- follow the ABC format

ABC Format

Actor

shall

Process / Manage / Perform / Execute

Behavior

within

Constraint

Examples

The Authentication Service shall authenticate a registered user within two seconds.

The Simulation Engine shall calculate prey movement within one simulation cycle.

Number requirements hierarchically.

Example

FR-1.1.1

FR-1.1.2

FR-2.3.1

Generate

| Requirement ID | Level-2 Capability | Functional Requirement |

---

# 9. Quality Requirements

Generate measurable quality requirements including (where applicable)

Performance

Reliability

Availability

Maintainability

Scalability

Usability

Security

Portability

Interoperability

Testability

AI Explainability (if applicable)

AI Safety (if applicable)

Each quality requirement shall be measurable whenever possible.

---

# 10. Performance Requirements

Generate measurable performance requirements.

Examples

Maximum response time

Memory usage

CPU utilization

Concurrent users

Simulation update rate

Database throughput

AI inference latency

Only include applicable requirements.

---

# 11. Assumptions

List all assumptions.

---

# 12. Constraints

List technical constraints.

Examples

Programming language

Operating system

Database

Framework

Hardware

External APIs

---

# 13. External Interfaces

Identify

User Interfaces

Hardware Interfaces

Software Interfaces

Communication Interfaces

External Services

---

# 14. Requirements Traceability Matrix

Generate a traceability matrix.

Every functional requirement SHALL trace back to exactly one Level-2 capability.

| Requirement ID | Level-2 Capability | Requirement Description |

---

# 15. Future Versions

Describe

Version 1

Version 2

Version 3

Future enhancements

---

# 16. Open Issues

List unresolved questions.

Do NOT invent answers.

Mark them clearly.

---

# 17. Glossary

Generate a glossary of important project terminology.

---

# General Requirements

The document shall

- remain professionally written
- use Markdown formatting
- be GitHub friendly
- maintain version history
- preserve previous work
- improve incrementally
- remain synchronized with the GitHub repository

Do NOT remove previously documented requirements unless they are obsolete.

Do NOT invent functionality that does not exist in the repository.

Whenever uncertain, identify the item as

> **To Be Completed**

rather than making assumptions.

The final document should resemble a professional Product Requirements Document maintained throughout the software development lifecycle rather than a one-time homework submission.
