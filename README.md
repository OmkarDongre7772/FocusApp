#  FocusApp — Digital Workplace Wellness Monitor

### Focus & Fragmentation Tracker (Privacy-First Productivity Analytics)

**FocusApp** is a Windows-based background service that monitors work activity patterns to help individuals and organizations improve deep work, reduce distractions, and build healthier digital habits — all while preserving user privacy.
---

## Vision

To become a trusted productivity intelligence platform that enables healthier work habits, reduced distraction, and data-driven performance improvement — without compromising user privacy.

##  Mission

Help individuals and organizations improve productivity and deep work through privacy-first focus analytics, fragmentation measurement, and actionable insights.
---

##  Key Features

✔️ Works offline — no constant internet required
✔️ Privacy-first (no screenshots, keystrokes, or content tracking)
✔️ Converts distractions into measurable metrics
✔️ Detects focus sessions automatically
✔️ Tracks idle time and break patterns
✔️ Fragmentation scoring based on context switching
✔️ Employee productivity dashboard
✔️ Manager analytics with aggregated insights only
✔️ Lightweight background Windows service

##  Proposed Solution

A **Windows Focus & Fragmentation Tracker** that runs as a background service and analyzes user activity patterns.

### Core Components

* 🔄 **Background Service** — Runs continuously, auto-starts on boot
* 📱 **Activity Tracking** — Detects active apps and foreground windows
* ⏳ **Idle Detection** — Identifies inactivity and breaks
* 🧩 **Focus Session Engine** — State machine to manage sessions
* 📉 **Fragmentation Scoring** — Measures interruptions & switching
* 💾 **Local Storage** — SQLite database for session logs
* 📊 **Employee Dashboard** — Personal productivity insights
* 🏢 **Manager Dashboard** — Team-level aggregated analytics

---

## 🔐 Privacy & Ethics

Signal Pulse is designed as an **ethical productivity tool**, not surveillance software.

**We DO NOT collect:**

* ❌ Screenshots
* ❌ Keystrokes
* ❌ Personal content
* ❌ Website data
* ❌ Files or messages

**We DO collect:**

* ✅ App usage patterns
* ✅ Active/idle time
* ✅ Context switching frequency
* ✅ Focus session duration

Manager reports are aggregated — no individual spying.
---

## 🏗️ System Workflow

1. Service starts automatically on system boot
2. Tracks foreground window activity
3. Detects idle periods
4. Identifies focus sessions
5. Calculates fragmentation score
6. Stores data locally
7. Generates analytics dashboards

---
## ⚙️ Tech Stack

### Core Technologies

* **Language:** C#
* **Framework:** .NET (Worker Service + WPF UI)
* **UI:** WPF Charts & Components
* **Database:** SQLite
* **System APIs:** Windows Win32 API
* **Cloud:** Supabase
* **Security:** Local encrypted settings

---

## 🧩 Architecture

Modular architecture with clear separation:
```
Service Layer → Activity Tracking → Focus Engine → Database → Analytics → UI Dashboard
```
---


## 📈 Impact & Benefits

### 👤 For Employees

* Increased awareness of distractions
* Improved time management
* Better deep work habits
* Reduced burnout

### 👥 For Managers

* Team productivity insights without surveillance
* Meeting overload detection
* Improved workload planning

### 🏢 For Organizations

* Higher efficiency
* Healthier work culture
* Ethical monitoring framework
