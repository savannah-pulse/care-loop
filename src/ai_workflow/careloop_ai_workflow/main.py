from fastapi import FastAPI

from careloop_ai_workflow.workflow import WORKFLOW_STEPS, WorkflowStep

app = FastAPI(
    title="CareLoop AI workflow",
    description="Python coordination workflow scaffold; no clinical decisions or case writes.",
    version="0.1.0",
)


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok"}


@app.get("/workflow/stages", response_model=list[WorkflowStep])
def workflow_stages() -> list[WorkflowStep]:
    return list(WORKFLOW_STEPS)
