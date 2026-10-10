import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import type { SlaStatus } from "../../../types/api";
import { SlaCell } from "./SlaCell";

const cases: [SlaStatus, string][] = [
  ["NotApplicable", "No SLA"],
  ["WithinSla", "Within SLA"],
  ["AtRisk", "At Risk"],
  ["Breached", "Breached"],
  ["Met", "Met"],
];

describe("SlaCell", () => {
  it.each(cases)("shows the label for %s", (status, label) => {
    render(
      <SlaCell
        slaStatus={status}
        dueAtUtc={status === "NotApplicable" ? null : "2026-10-11T12:00:00Z"}
      />,
    );

    expect(screen.getByText(label)).toBeInTheDocument();
  });
});
