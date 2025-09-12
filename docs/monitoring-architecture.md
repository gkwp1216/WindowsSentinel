# Monitoring Architecture (Draft)

## 1. Purpose & Scope

- Define the end-to-end architecture for always-on network monitoring in Windows Sentinel.
- Cover capture pipeline, threading/backpressure, storage, UI integration, and fallback behavior.

## 2. System Overview

```
PacketCapture (SharpPcap)
  -> Parser (PacketDotNet)
    -> ProcessNetworkMapper (PID/Port -> Process)
      -> RealTimeSecurityAnalyzer (Rules/Thresholds)
        -> Sinks: UI (Grid/Charts/Alerts), Storage (SQLite, batch)
Fallback: EventLog-based lightweight monitoring
```

## 3. Components

### 3.1 PacketCapture

- Active NIC auto-selection, BPF filter support (TCP/UDP/ICMP)
- Start/Stop, error handling with exponential backoff restart
- Metrics: drop counters, throughput

### 3.2 Parser

- Extract: ts, proto, src/dst ip/port, length, flags

### 3.3 ProcessNetworkMapper

- PID/port cache, refresh interval, cleanup of terminated processes
- Retry strategies; degraded mode messaging when not elevated

### 3.4 RealTimeSecurityAnalyzer

- Rules: suspicious ports/domains/IPs, abnormal patterns, large transfers
- Severity scoring; de-dup (hash key + debounce)

### 3.5 Storage (SQLite)

- Tables: connections, alerts, processes
- Async batch commits; retention (rolling delete), WAL/Journal for recovery

### 3.6 UI Integration (NetWorks_New)

- Live grid/chart updates; filters (protocol/process/time)
- Status (capturing/idle), hints, toast/tray alerts

### 3.7 Fallback (EventLog)

- When Npcap missing or insufficient privileges, provide limited insights

## 4. Threading & Backpressure

- Background capture/parse/analyze threads; UI updates on Dispatcher
- Bounded channels/queues; sampling when above thresholds
- Metrics: queue length, drop rate, restart count

## 5. Data Model

- connections(id, ts, proto, src_ip, src_port, dst_ip, dst_port, length, pid, process_name, risk_level)
- alerts(id, ts, severity, title, description, src_ip, dst_ip, src_port, dst_port, rule_id, dedup_key)
- processes(pid, name, path, signer, last_seen)

## 6. Lifecycle & Recovery

- App start: check Npcap/privileges -> select NIC -> start capture (optional)
- Sleep/resume, NIC changes: auto stop/resume
- Exceptions: log and restart with backoff; guide user after repeated failures

## 7. Settings & Policies

- capture.interface (auto/specific), capture.bpf
- retention.hours, retention.maxRows
- alert.enabled, alert.threshold, notify.tray, notify.toast
- perf.maxQueue, perf.samplingRate

## 8. Performance Targets

- CPU ≤ 2–5% (typical usage), Memory ≤ 150MB
- Recovery within 5s after NIC change/resume

## 9. Testing Strategy

- Unit: parser, rules, retention
- Integration: sample pcap replay; high load sampling/backpressure
- Scenarios: first run, auto-start, sleep/resume, NIC change, Npcap missing

## 10. Risks & Mitigations

- Npcap/privilege issues -> fallback + guidance
- High traffic -> sampling/windowing; UI virtualization
- PID/port mapping -> cache/retry/partial display

## 11. Open Questions

- Exact default thresholds for alerts?
- Retention defaults (hours vs rows)?
- Tray/Toast behavior in non-elevated mode?

---

Status: Draft. Iteratively refine during Sprint 1.

## 12. Contracts & Data Types (Draft)

- PacketDto
  - ts: DateTime
  - proto: enum { TCP, UDP, ICMP, Other }
  - srcIp: string, srcPort: int?
  - dstIp: string, dstPort: int?
  - length: int
  - flags: uint (proto-specific)
- FlowRecord
  - id: string (hash(ts, 5-tuple, pid))
  - ts: DateTime
  - fiveTuple: (proto, srcIp, srcPort, dstIp, dstPort)
  - pid: int?, processName: string?
  - length: int, direction: enum { Inbound, Outbound, Local }
  - riskLevel: enum { Low, Medium, High, Critical }
- SecurityAlert
  - id: string, ts: DateTime, severity: enum { Low, Medium, High, Critical }
  - title: string, description: string, ruleId: string, dedupKey: string
  - context: { srcIp, srcPort?, dstIp, dstPort?, pid?, processName? }

Interfaces

- ICaptureService
  - StartAsync(filter: string?, nicId: string?); StopAsync(); IsRunning: bool
  - Events: OnPacket(PacketDto), OnError(Exception), OnMetrics({drop, qlen, tput})
- IProcessMapper
  - ResolveAsync(proto, localIp, localPort, remoteIp, remotePort) -> (pid?, processName?)
- IAnalyzer
  - Evaluate(PacketDto or FlowRecord) -> IEnumerable<SecurityAlert>
- IStorage
  - SaveConnectionsAsync(IEnumerable<FlowRecord>) (batch)
  - SaveAlertsAsync(IEnumerable<SecurityAlert>) (batch)
  - PruneAsync(retention)

## 13. Threading Parameters & Queues

- Channels
  - capture -> parse: capacity = 10_000 (bounded, drop oldest)
  - parse -> map: capacity = 5_000
  - map -> analyze: capacity = 5_000
  - analyze -> sink(storage/ui): capacity = 5_000
- Sampling
  - enable when any queue > 80%: sample 1 of N (N adaptive by load)
- UI Update
  - throttle to 10–20 Hz; coalesce updates (batch into ObservableCollection)
- Metrics
  - queue length per stage, drop count, restart count, avg processing time

## 14. Error Modes & Recovery

- Npcap missing or no privileges
  - Mode: Fallback(EventLog); Notify with action link (install/run as admin)
- Device down or NIC change
  - Action: Stop capture; re-select active NIC; restart within ≤5s
- Filter compile error / open failure
  - Action: revert to broader filter; log and notify
- High load (sustained drops)
  - Action: increase sampling; raise warning; persist only summaries
- Storage busy/locked
  - Action: queue to memory with cap; retry with backoff; shed oldest when full

## 15. Default Settings (Proposed)

- capture.interface = "auto"
- capture.bpf = "tcp or udp or icmp"
- retention.hours = 24, retention.maxRows = 1_000_000
- alert.enabled = true, alert.threshold = "medium"
- notify.tray = true, notify.toast = true
- perf.maxQueue = 10_000, perf.samplingRate = auto
- ui.maxRowsLive = 2_000, ui.chartWindow = 60s

## 16. Test Matrix (Initial)

- Functional
  - Start/Stop capture (ok/error paths), NIC auto-select, BPF filter effects
  - Process mapping with/without admin
  - Analyzer rules trigger/dedupe behavior
- Performance
  - Idle vs high-throughput (iperf) CPU/Memory; queue/drops under load
  - UI throttle effect; live rows limit, chart windowing
- Resilience
  - NIC change during capture; sleep/resume; Npcap uninstall mid-run
  - Storage lock/failure; automatic retry/backoff; memory cap shedding
- Persistence
  - Retention pruning at thresholds; index usage; recovery after crash
