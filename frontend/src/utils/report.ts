import type { EventRecord, SimulationDataset, StateTransitionRecord } from '../types';

function tableRow(label: string, value: string | number): string {
  return `| ${label} | ${String(value).replace(/\|/g, '\\|')} |`;
}

export function buildSimulationReport(dataset: SimulationDataset): string {
  const { metadata, summary, records, source, warnings } = dataset;
  const events = records.filter((record): record is EventRecord => record.record_type === 'event');
  const transitions = records.filter(
    (record): record is StateTransitionRecord => record.record_type === 'state_transition',
  );
  const lines = [
    '# AeroGuard Simulation Report',
    '',
    "> The dashboard built this report from the C++ simulator's JSONL artifact. It doesn't recreate simulation decisions.",
    '',
    '## Run metadata',
    '',
    '| Field | Recorded value |',
    '| --- | --- |',
    tableRow('Scenario', metadata.scenario),
    tableRow('Description', metadata.description),
    tableRow('Seed', metadata.seed),
    tableRow('Tick interval', `${metadata.tick_ms} ms`),
    tableRow('Duration', `${metadata.duration_ms} ms`),
    tableRow('Source', source),
    tableRow('Schema version', metadata.schema_version),
    '',
    '## Recorded summary',
    '',
  ];

  if (summary) {
    lines.push(
      '| Metric | Recorded value |',
      '| --- | --- |',
      tableRow('Final system state', summary.final_state),
      tableRow('Ticks', summary.ticks),
      tableRow('Telemetry records', summary.telemetry_count),
      tableRow('Invalid telemetry', summary.invalid_count),
      tableRow('Missing heartbeats', summary.missing_heartbeat_count),
      tableRow('Dropped messages', summary.dropped_message_count),
      tableRow('State transitions', summary.state_transition_count),
    );
  } else {
    lines.push('The source artifact has no summary record.');
  }

  lines.push('', '## State transitions', '');
  if (transitions.length) {
    lines.push('| Time | From | To | Severity | Reason |', '| ---: | --- | --- | --- | --- |');
    transitions.forEach((record) => lines.push(
      `| ${record.timestamp_ms} ms | ${record.from} | ${record.to} | ${record.severity} | ${record.reason.replace(/\|/g, '\\|')} |`,
    ));
  } else {
    lines.push('The simulator recorded no state transitions.');
  }

  lines.push('', '## Events', '');
  if (events.length) {
    lines.push('| Time | Severity | Code | Subsystem | Message |', '| ---: | --- | --- | --- | --- |');
    events.forEach((record) => lines.push(
      `| ${record.timestamp_ms} ms | ${record.severity} | ${record.event_code} | ${record.subsystem ?? 'System'} | ${record.message.replace(/\|/g, '\\|')} |`,
    ));
  } else {
    lines.push('The simulator recorded no events.');
  }

  if (warnings.length) {
    lines.push('', '## Import warnings', '', ...warnings.map((warning) => `- ${warning}`));
  }

  lines.push('', '---', '', 'AeroGuard is an independent educational simulator with no aircraft-use certification.');
  return `${lines.join('\n')}\n`;
}

export function downloadSimulationReport(dataset: SimulationDataset): void {
  const content = buildSimulationReport(dataset);
  const blob = new Blob([content], { type: 'text/markdown;charset=utf-8' });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = `aeroguard-${dataset.metadata.scenario.replace(/[^a-z0-9]+/gi, '-').toLowerCase()}-report.md`;
  document.body.append(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}
