import { type NextRequest } from "next/server";
import { proxyToApi } from "@/lib/apiProxy";

type Ctx = { params: { path?: string[] } };

export async function GET(req: NextRequest, ctx: Ctx) {
  return proxyToApi(req, ctx.params.path);
}
export async function POST(req: NextRequest, ctx: Ctx) {
  return proxyToApi(req, ctx.params.path);
}
export async function PUT(req: NextRequest, ctx: Ctx) {
  return proxyToApi(req, ctx.params.path);
}
export async function PATCH(req: NextRequest, ctx: Ctx) {
  return proxyToApi(req, ctx.params.path);
}
export async function DELETE(req: NextRequest, ctx: Ctx) {
  return proxyToApi(req, ctx.params.path);
}

export const dynamic = "force-dynamic";
