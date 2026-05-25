import { redirect } from "next/navigation";
import { serverFetchApi } from "@/lib/serverApi";
import { RunSession, type RunBlock, type RunDrill } from "@/components/RunSession";

interface SessionDto {
  id: string;
  teamId: string;
  focus: string | null;
  sourceSessionPlanId?: string | null;
}

interface PlanBlockDto {
  block: string;
  title: string;
  durationMinutes: number;
  intent: string;
  intensity: string;
  glossary?: { term: string; plain: string }[];
}

interface RecommendationDto {
  drillId: string;
  title: string;
  description: string;
  durationMinutes: number;
  rationale: string;
  tags: string[];
  longDescription?: string | null;
  coachingCues?: string[] | null;
  equipment?: string[] | null;
  whatItMeans?: string | null;
  diagramKey?: string | null;
  glossary?: { term: string; plain: string }[] | null;
}

interface SessionPlanDto {
  id: string;
  focus: string;
  blocks: PlanBlockDto[];
  recommendations: RecommendationDto[];
}

export const dynamic = "force-dynamic";
export const metadata = { title: "Run session — ForgeRise" };

export default async function RunSessionPage({
  params,
}: {
  params: Promise<{ teamId: string; sessionId: string }>;
}) {
  const { teamId, sessionId } = await params;

  const sessionResp = await serverFetchApi<SessionDto>(
    `/teams/${teamId}/sessions/${sessionId}`,
  );
  if (!sessionResp.ok) {
    if (sessionResp.status === 401) redirect("/login");
    redirect(`/teams/${teamId}`);
  }
  const session = sessionResp.data;
  // Run mode only makes sense for adopted sessions — we need the underlying
  // plan blocks + drills to walk through. Bounce non-adopted sessions back
  // to the detail page where the user is told why.
  if (!session.sourceSessionPlanId) {
    redirect(`/teams/${teamId}/sessions/${sessionId}`);
  }

  const planResp = await serverFetchApi<SessionPlanDto>(
    `/teams/${teamId}/session-plans/${session.sourceSessionPlanId}`,
  );
  if (!planResp.ok) {
    redirect(`/teams/${teamId}/sessions/${sessionId}`);
  }
  const plan = planResp.data;

  const blocks: RunBlock[] = plan.blocks.map((b) => ({
    block: b.block,
    title: b.title,
    durationMinutes: b.durationMinutes,
    intent: b.intent,
    intensity: b.intensity,
    glossary: b.glossary,
  }));
  const drills: RunDrill[] = plan.recommendations.map((r) => ({
    drillId: r.drillId,
    title: r.title,
    description: r.description,
    durationMinutes: r.durationMinutes,
    rationale: r.rationale,
    tags: r.tags,
    longDescription: r.longDescription ?? null,
    coachingCues: r.coachingCues ?? null,
    equipment: r.equipment ?? null,
    whatItMeans: r.whatItMeans ?? null,
    diagramKey: r.diagramKey ?? null,
    glossary: r.glossary ?? null,
  }));

  return (
    <main className="min-h-screen bg-mist-grey">
      <RunSession
        teamId={teamId}
        sessionId={sessionId}
        focus={session.focus ?? plan.focus}
        blocks={blocks}
        drills={drills}
      />
    </main>
  );
}
