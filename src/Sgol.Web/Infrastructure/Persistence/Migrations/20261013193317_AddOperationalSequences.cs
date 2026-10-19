using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgol.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationalSequences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_operational_assignment_Id_PlanId",
                table: "operational_assignment",
                columns: new[] { "Id", "PlanId" });

            migrationBuilder.CreateTable(
                name: "operational_sequence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operational_sequence", x => x.Id);
                    table.CheckConstraint("CK_sequence_revision", "\"Revision\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "operational_sequence_version",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SequenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    StartKind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    FirstMonth = table.Column<int>(type: "integer", nullable: false),
                    WindowFromDay = table.Column<int>(type: "integer", nullable: false),
                    WindowThroughDay = table.Column<int>(type: "integer", nullable: false),
                    PreferredWeekDay = table.Column<int>(type: "integer", nullable: false),
                    StartHour = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operational_sequence_version", x => x.Id);
                    table.CheckConstraint("CK_sequence_calendar", "\"Number\" > 0 AND \"StartKind\" IN ('SEMESTRAL','OCASIONAL') AND \"FirstMonth\" BETWEEN 1 AND 6 AND \"WindowFromDay\" BETWEEN 1 AND 28 AND \"WindowThroughDay\" BETWEEN \"WindowFromDay\" AND 28 AND \"PreferredWeekDay\" BETWEEN 0 AND 6 AND \"StartHour\" BETWEEN 0 AND 23");
                    table.ForeignKey(
                        name: "FK_operational_sequence_version_app_user_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_operational_sequence_version_operational_sequence_SequenceId",
                        column: x => x.SequenceId,
                        principalTable: "operational_sequence",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "operational_sequence_step",
                columns: table => new
                {
                    VersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ReleaseCondition = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    DeadlineKind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    DeadlineHours = table.Column<int>(type: "integer", nullable: false),
                    MinimumStartOffsetHours = table.Column<int>(type: "integer", nullable: true),
                    ReturnToPosition = table.Column<int>(type: "integer", nullable: true),
                    Instructions = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operational_sequence_step", x => new { x.VersionId, x.Position });
                    table.CheckConstraint("CK_sequence_step_position", "\"Position\" BETWEEN 1 AND 32 AND (\"ReturnToPosition\" IS NULL OR \"ReturnToPosition\" BETWEEN 1 AND \"Position\" - 1)");
                    table.CheckConstraint("CK_sequence_step_rule", "\"Action\" IN ('ENTREGAR','REVISAR','SELLAR','AUTORIZAR') AND ((\"Position\" = 1 AND \"ReleaseCondition\" = 'INICIO') OR (\"Position\" > 1 AND \"ReleaseCondition\" IN ('ENTREGA','APROBACION'))) AND \"DeadlineKind\" IN ('RELATIVO','FIJO') AND \"DeadlineHours\" BETWEEN 1 AND 8760 AND (\"MinimumStartOffsetHours\" IS NULL OR \"MinimumStartOffsetHours\" BETWEEN 0 AND 8760)");
                    table.ForeignKey(
                        name: "FK_operational_sequence_step_operational_sequence_version_Vers~",
                        column: x => x.VersionId,
                        principalTable: "operational_sequence_version",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_operational_sequence_step_operational_task_TaskId",
                        column: x => x.TaskId,
                        principalTable: "operational_task",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "operational_sequence_attempt",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    StageId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Round = table.Column<int>(type: "integer", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    InputDocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    OutputDocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Comment = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    UnresolvedDifferences = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operational_sequence_attempt", x => x.Id);
                    table.UniqueConstraint("AK_operational_sequence_attempt_RunId_Id", x => new { x.RunId, x.Id });
                    table.UniqueConstraint("AK_operational_sequence_attempt_RunId_StageId_Id", x => new { x.RunId, x.StageId, x.Id });
                    table.CheckConstraint("CK_sequence_attempt_state", "\"Number\" > 0 AND \"Round\" > 0 AND \"Revision\" > 0 AND \"Status\" IN ('PREPARADO','ACTIVO','ENTREGADO','APROBADO','SUSTITUIDO','CANCELADO') AND (\"Status\" NOT IN ('ACTIVO','ENTREGADO','APROBADO') OR \"AssignmentId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_operational_sequence_attempt_operational_assignment_Assignm~",
                        columns: x => new { x.AssignmentId, x.PlanId },
                        principalTable: "operational_assignment",
                        principalColumns: new[] { "Id", "PlanId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "operational_sequence_document",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProducerAttemptId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    PreviousDocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operational_sequence_document", x => x.Id);
                    table.UniqueConstraint("AK_operational_sequence_document_RunId_Id", x => new { x.RunId, x.Id });
                    table.CheckConstraint("CK_sequence_document_version", "\"Number\" > 0 AND \"Sha256\" ~ '^[0-9a-f]{64}$' AND (\"PreviousDocumentId\" IS NULL OR \"PreviousDocumentId\" <> \"Id\")");
                    table.ForeignKey(
                        name: "FK_operational_sequence_document_app_user_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_operational_sequence_document_operational_attachment_Assign~",
                        column: x => x.AssignmentId,
                        principalTable: "operational_attachment",
                        principalColumn: "AssignmentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_operational_sequence_document_operational_sequence_attempt_~",
                        columns: x => new { x.RunId, x.ProducerAttemptId },
                        principalTable: "operational_sequence_attempt",
                        principalColumns: new[] { "RunId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_operational_sequence_document_operational_sequence_document~",
                        columns: x => new { x.RunId, x.PreviousDocumentId },
                        principalTable: "operational_sequence_document",
                        principalColumns: new[] { "RunId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "operational_sequence_event",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    StageId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    EventKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorKind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Comment = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operational_sequence_event", x => x.Id);
                    table.UniqueConstraint("AK_operational_sequence_event_RunId_Id", x => new { x.RunId, x.Id });
                    table.CheckConstraint("CK_sequence_event_actor", "(\"ActorKind\" = 'USER' AND \"ActorId\" IS NOT NULL) OR (\"ActorKind\" = 'SYSTEM' AND \"ActorId\" IS NULL)");
                    table.CheckConstraint("CK_sequence_event_kind", "\"Kind\" IN ('INICIO','HABILITACION','ASIGNACION','ENTREGA','APROBACION','DEVOLUCION','REASIGNACION','CIERRE','CANCELACION','BLOQUEO')");
                    table.ForeignKey(
                        name: "FK_operational_sequence_event_app_user_ActorId",
                        column: x => x.ActorId,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_operational_sequence_event_operational_sequence_attempt_Run~",
                        columns: x => new { x.RunId, x.StageId, x.AttemptId },
                        principalTable: "operational_sequence_attempt",
                        principalColumns: new[] { "RunId", "StageId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_operational_sequence_event_operational_sequence_document_Ru~",
                        columns: x => new { x.RunId, x.DocumentId },
                        principalTable: "operational_sequence_document",
                        principalColumns: new[] { "RunId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "operational_sequence_run",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    ActivePosition = table.Column<int>(type: "integer", nullable: false),
                    Round = table.Column<int>(type: "integer", nullable: false),
                    PlannedStartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ClosureOutcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ClosureComment = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    AuthorizedDocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operational_sequence_run", x => x.Id);
                    table.UniqueConstraint("AK_operational_sequence_run_Id_PlanId", x => new { x.Id, x.PlanId });
                    table.UniqueConstraint("AK_operational_sequence_run_Id_VersionId", x => new { x.Id, x.VersionId });
                    table.CheckConstraint("CK_sequence_run_closure", "(\"Status\" = 'EN_CURSO' AND \"ClosedAt\" IS NULL AND \"ClosedBy\" IS NULL AND \"ClosureOutcome\" IS NULL AND \"AuthorizedDocumentId\" IS NULL) OR (\"Status\" = 'CONCLUIDA' AND \"ClosedAt\" IS NOT NULL AND \"ClosedBy\" IS NOT NULL AND \"AuthorizedDocumentId\" IS NOT NULL AND \"ClosureOutcome\" IS NOT NULL AND \"ClosureComment\" IS NOT NULL AND \"ClosureOutcome\" IN ('SIN_DIFERENCIAS','ACEPTADA_CON_DIFERENCIAS') AND length(trim(\"ClosureComment\")) > 0) OR (\"Status\" = 'CANCELADA' AND \"ClosedAt\" IS NOT NULL AND \"ClosedBy\" IS NOT NULL AND \"ClosureOutcome\" = 'CANCELACION' AND \"ClosureOutcome\" IS NOT NULL AND \"ClosureComment\" IS NOT NULL AND length(trim(\"ClosureComment\")) > 0 AND \"AuthorizedDocumentId\" IS NULL)");
                    table.CheckConstraint("CK_sequence_run_state", "\"Status\" IN ('EN_CURSO','CONCLUIDA','CANCELADA') AND \"Round\" > 0 AND \"ActivePosition\" BETWEEN 1 AND 32 AND \"Revision\" > 0");
                    table.ForeignKey(
                        name: "FK_operational_sequence_run_app_user_ClosedBy",
                        column: x => x.ClosedBy,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_operational_sequence_run_app_user_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_operational_sequence_run_direction_prepared_plan_PlanId",
                        column: x => x.PlanId,
                        principalTable: "direction_prepared_plan",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_operational_sequence_run_operational_sequence_version_Versi~",
                        column: x => x.VersionId,
                        principalTable: "operational_sequence_version",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sequence_run_authorized_document",
                        columns: x => new { x.Id, x.AuthorizedDocumentId },
                        principalTable: "operational_sequence_document",
                        principalColumns: new[] { "RunId", "Id" });
                });

            migrationBuilder.CreateTable(
                name: "operational_sequence_stage",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskRevision = table.Column<long>(type: "bigint", nullable: false),
                    TaskCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Requirement = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    SpecializationId = table.Column<Guid>(type: "uuid", nullable: true),
                    SpecializationName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    LoadMinutes = table.Column<int>(type: "integer", nullable: false),
                    MinimumPhotos = table.Column<int>(type: "integer", nullable: false),
                    MaximumPhotos = table.Column<int>(type: "integer", nullable: false),
                    MaximumPhotoBytes = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CurrentAttemptId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReservedPersonId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReservedPersonName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    PlannedStartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PlannedDueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    MinimumStartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FixedDueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DependencySatisfiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DependencyEventId = table.Column<Guid>(type: "uuid", nullable: true),
                    EligibleAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EnabledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EffectiveDueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    BlockReason = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operational_sequence_stage", x => x.Id);
                    table.UniqueConstraint("AK_operational_sequence_stage_RunId_Id", x => new { x.RunId, x.Id });
                    table.CheckConstraint("CK_sequence_stage_dates", "\"PlannedDueAt\" > \"PlannedStartsAt\" AND ((\"EnabledAt\" IS NULL AND \"EffectiveDueAt\" IS NULL) OR (\"EnabledAt\" IS NOT NULL AND \"EligibleAt\" IS NOT NULL AND \"EffectiveDueAt\" IS NOT NULL AND \"EffectiveDueAt\" > \"EnabledAt\" AND \"EnabledAt\" >= \"EligibleAt\"))");
                    table.CheckConstraint("CK_sequence_stage_dependency", "(\"DependencySatisfiedAt\" IS NULL AND \"DependencyEventId\" IS NULL) OR (\"DependencySatisfiedAt\" IS NOT NULL AND \"DependencyEventId\" IS NOT NULL)");
                    table.CheckConstraint("CK_sequence_stage_snapshot", "\"TaskRevision\" > 0 AND \"LoadMinutes\" BETWEEN 1 AND 4800 AND \"Role\" IN ('PISO_VENTAS','SUBCOORDINACION','ADMINISTRACION','DIRECCION') AND \"Requirement\" IN ('FOTOGRAFIA','CAPTURA_PANTALLA','COMENTARIO','DOCUMENTO','NINGUNO') AND \"MinimumPhotos\" BETWEEN 1 AND 5 AND \"MaximumPhotos\" BETWEEN \"MinimumPhotos\" AND 5 AND \"MaximumPhotoBytes\" BETWEEN 1048576 AND 31457280 AND \"Revision\" > 0");
                    table.CheckConstraint("CK_sequence_stage_state", "\"Status\" IN ('EN_ESPERA','PROGRAMADA','DISPONIBLE','PENDIENTE_ASIGNACION','FUERA_DE_PLAZO','CONCLUIDA','CANCELADA') AND (\"Status\" NOT IN ('DISPONIBLE','CONCLUIDA') OR (\"CurrentAttemptId\" IS NOT NULL AND \"EnabledAt\" IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_operational_sequence_stage_operational_sequence_run_RunId_V~",
                        columns: x => new { x.RunId, x.VersionId },
                        principalTable: "operational_sequence_run",
                        principalColumns: new[] { "Id", "VersionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_operational_sequence_stage_operational_sequence_step_Versio~",
                        columns: x => new { x.VersionId, x.Position },
                        principalTable: "operational_sequence_step",
                        principalColumns: new[] { "VersionId", "Position" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_operational_sequence_stage_operational_task_TaskId",
                        column: x => x.TaskId,
                        principalTable: "operational_task",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_operational_sequence_stage_person_ReservedPersonId",
                        column: x => x.ReservedPersonId,
                        principalTable: "person",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_operational_sequence_stage_specialization_SpecializationId",
                        column: x => x.SpecializationId,
                        principalTable: "specialization",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sequence_stage_current_attempt",
                        columns: x => new { x.RunId, x.Id, x.CurrentAttemptId },
                        principalTable: "operational_sequence_attempt",
                        principalColumns: new[] { "RunId", "StageId", "Id" });
                    table.ForeignKey(
                        name: "FK_sequence_stage_dependency_event",
                        columns: x => new { x.RunId, x.DependencyEventId },
                        principalTable: "operational_sequence_event",
                        principalColumns: new[] { "RunId", "Id" });
                });

            migrationBuilder.CreateIndex(
                name: "IX_operational_sequence_attempt_AssignmentId",
                table: "operational_sequence_attempt",
                column: "AssignmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_operational_sequence_attempt_AssignmentId_PlanId",
                table: "operational_sequence_attempt",
                columns: new[] { "AssignmentId", "PlanId" });

            migrationBuilder.CreateIndex(
                name: "IX_operational_sequence_attempt_RunId_InputDocumentId",
                table: "operational_sequence_attempt",
                columns: new[] { "RunId", "InputDocumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_operational_sequence_attempt_RunId_OutputDocumentId",
                table: "operational_sequence_attempt",
                columns: new[] { "RunId", "OutputDocumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_operational_sequence_attempt_RunId_PlanId",
                table: "operational_sequence_attempt",
                columns: new[] { "RunId", "PlanId" });

            migrationBuilder.CreateIndex(
                name: "IX_operational_sequence_attempt_StageId_Number",
                table: "operational_sequence_attempt",
                columns: new[] { "StageId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_operational_sequence_document_AssignmentId",
                table: "operational_sequence_document",
                column: "AssignmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_operational_sequence_document_CreatedBy",
                table: "operational_sequence_document",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_operational_sequence_document_RunId_Number",
                table: "operational_sequence_document",
                columns: new[] { "RunId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_operational_sequence_document_RunId_PreviousDocumentId",
                table: "operational_sequence_document",
                columns: new[] { "RunId", "PreviousDocumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_operational_sequence_document_RunId_ProducerAttemptId",
                table: "operational_sequence_document",
                columns: new[] { "RunId", "ProducerAttemptId" });

            migrationBuilder.CreateIndex(
                name: "IX_operational_sequence_event_ActorId",
                table: "operational_sequence_event",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_operational_sequence_event_RunId_DocumentId",
                table: "operational_sequence_event",
                columns: new[] { "RunId", "DocumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_operational_sequence_event_RunId_EventKey",
                table: "operational_sequence_event",
                columns: new[] { "RunId", "EventKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_operational_sequence_event_RunId_OccurredAt",
                table: "operational_sequence_event",
                columns: new[] { "RunId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_operational_sequence_event_RunId_StageId_AttemptId",
                table: "operational_sequence_event",
                columns: new[] { "RunId", "StageId", "AttemptId" });

            migrationBuilder.CreateIndex(
                name: "IX_operational_sequence_run_ClosedBy",
                table: "operational_sequence_run",
                column: "ClosedBy");

            migrationBuilder.CreateIndex(
                name: "IX_operational_sequence_run_CreatedBy",
                table: "operational_sequence_run",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_operational_sequence_run_Id_AuthorizedDocumentId",
                table: "operational_sequence_run",
                columns: new[] { "Id", "AuthorizedDocumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_operational_sequence_run_PlanId",
                table: "operational_sequence_run",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_operational_sequence_run_StartKey",
                table: "operational_sequence_run",
                column: "StartKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_operational_sequence_run_VersionId",
                table: "operational_sequence_run",
                column: "VersionId");

            migrationBuilder.CreateIndex(
                name: "IX_operational_sequence_stage_CurrentAttemptId",
                table: "operational_sequence_stage",
                column: "CurrentAttemptId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_operational_sequence_stage_ReservedPersonId",
                table: "operational_sequence_stage",
                column: "ReservedPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_operational_sequence_stage_RunId_DependencyEventId",
                table: "operational_sequence_stage",
                columns: new[] { "RunId", "DependencyEventId" });

            migrationBuilder.CreateIndex(
                name: "IX_operational_sequence_stage_RunId_Id_CurrentAttemptId",
                table: "operational_sequence_stage",
                columns: new[] { "RunId", "Id", "CurrentAttemptId" });

            migrationBuilder.CreateIndex(
                name: "IX_operational_sequence_stage_RunId_Position",
                table: "operational_sequence_stage",
                columns: new[] { "RunId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_operational_sequence_stage_RunId_VersionId",
                table: "operational_sequence_stage",
                columns: new[] { "RunId", "VersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_operational_sequence_stage_SpecializationId",
                table: "operational_sequence_stage",
                column: "SpecializationId");

            migrationBuilder.CreateIndex(
                name: "IX_operational_sequence_stage_TaskId",
                table: "operational_sequence_stage",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_operational_sequence_stage_VersionId_Position",
                table: "operational_sequence_stage",
                columns: new[] { "VersionId", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_operational_sequence_step_TaskId",
                table: "operational_sequence_step",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_operational_sequence_version_CreatedBy",
                table: "operational_sequence_version",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_operational_sequence_version_SequenceId_Number",
                table: "operational_sequence_version",
                columns: new[] { "SequenceId", "Number" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_operational_sequence_attempt_operational_sequence_run_RunId~",
                table: "operational_sequence_attempt",
                columns: new[] { "RunId", "PlanId" },
                principalTable: "operational_sequence_run",
                principalColumns: new[] { "Id", "PlanId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_operational_sequence_attempt_operational_sequence_stage_Run~",
                table: "operational_sequence_attempt",
                columns: new[] { "RunId", "StageId" },
                principalTable: "operational_sequence_stage",
                principalColumns: new[] { "RunId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_sequence_attempt_input_document",
                table: "operational_sequence_attempt",
                columns: new[] { "RunId", "InputDocumentId" },
                principalTable: "operational_sequence_document",
                principalColumns: new[] { "RunId", "Id" });

            migrationBuilder.AddForeignKey(
                name: "FK_sequence_attempt_output_document",
                table: "operational_sequence_attempt",
                columns: new[] { "RunId", "OutputDocumentId" },
                principalTable: "operational_sequence_document",
                principalColumns: new[] { "RunId", "Id" });

            // Pointer/document cycles are validated at commit, allowing one atomic return.
            migrationBuilder.Sql("""
                ALTER TABLE operational_sequence_stage ALTER CONSTRAINT "FK_sequence_stage_current_attempt" DEFERRABLE INITIALLY DEFERRED;
                ALTER TABLE operational_sequence_stage ALTER CONSTRAINT "FK_sequence_stage_dependency_event" DEFERRABLE INITIALLY DEFERRED;
                ALTER TABLE operational_sequence_attempt ALTER CONSTRAINT "FK_sequence_attempt_input_document" DEFERRABLE INITIALLY DEFERRED;
                ALTER TABLE operational_sequence_attempt ALTER CONSTRAINT "FK_sequence_attempt_output_document" DEFERRABLE INITIALLY DEFERRED;
                ALTER TABLE operational_sequence_run ALTER CONSTRAINT "FK_sequence_run_authorized_document" DEFERRABLE INITIALLY DEFERRED;
                ALTER TABLE operational_sequence_stage ADD CONSTRAINT "EX_sequence_single_actionable_stage"
                    EXCLUDE USING gist ("RunId" WITH =)
                    WHERE ("Status" IN ('DISPONIBLE','PROGRAMADA','PENDIENTE_ASIGNACION','FUERA_DE_PLAZO'))
                    DEFERRABLE INITIALLY DEFERRED;

                CREATE FUNCTION sgol_sequence_version_immutable() RETURNS trigger LANGUAGE plpgsql AS $body$
                DECLARE v_id uuid;
                BEGIN
                    IF TG_TABLE_NAME = 'operational_sequence_version' THEN v_id := OLD."Id";
                    ELSIF TG_OP = 'DELETE' THEN v_id := OLD."VersionId";
                    ELSE v_id := NEW."VersionId";
                    END IF;
                    IF EXISTS (SELECT 1 FROM operational_sequence_run WHERE "VersionId" = v_id) THEN
                        RAISE EXCEPTION 'A version used by a sequence execution is immutable' USING ERRCODE = '23514';
                    END IF;
                    IF TG_TABLE_NAME = 'operational_sequence_step' AND TG_OP = 'UPDATE' THEN
                        IF EXISTS (SELECT 1 FROM operational_sequence_run WHERE "VersionId" = OLD."VersionId") THEN
                            RAISE EXCEPTION 'A version used by a sequence execution is immutable' USING ERRCODE = '23514';
                        END IF;
                    END IF;
                    IF TG_OP = 'DELETE' THEN RETURN OLD; ELSE RETURN NEW; END IF;
                END;
                $body$;
                CREATE TRIGGER sequence_version_immutable BEFORE UPDATE OR DELETE ON operational_sequence_version
                    FOR EACH ROW EXECUTE FUNCTION sgol_sequence_version_immutable();
                CREATE TRIGGER sequence_step_immutable BEFORE INSERT OR UPDATE OR DELETE ON operational_sequence_step
                    FOR EACH ROW EXECUTE FUNCTION sgol_sequence_version_immutable();

                CREATE FUNCTION sgol_sequence_document_producer() RETURNS trigger LANGUAGE plpgsql AS $body$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM operational_sequence_attempt a
                        WHERE a."RunId" = NEW."RunId" AND a."Id" = NEW."ProducerAttemptId"
                            AND a."AssignmentId" = NEW."AssignmentId") THEN
                        RAISE EXCEPTION 'Document does not belong to its producer assignment' USING ERRCODE = '23503';
                    END IF;
                    RETURN NEW;
                END;
                $body$;
                CREATE CONSTRAINT TRIGGER sequence_document_producer AFTER INSERT OR UPDATE ON operational_sequence_document
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION sgol_sequence_document_producer();

                CREATE FUNCTION sgol_sequence_append_only() RETURNS trigger LANGUAGE plpgsql AS $body$
                BEGIN
                    RAISE EXCEPTION 'Sequence documents and events are append only' USING ERRCODE = '23514';
                END;
                $body$;
                CREATE TRIGGER sequence_document_append_only BEFORE UPDATE OR DELETE ON operational_sequence_document
                    FOR EACH ROW EXECUTE FUNCTION sgol_sequence_append_only();
                CREATE TRIGGER sequence_event_append_only BEFORE UPDATE OR DELETE ON operational_sequence_event
                    FOR EACH ROW EXECUTE FUNCTION sgol_sequence_append_only();

                CREATE FUNCTION sgol_sequence_attempt_identity() RETURNS trigger LANGUAGE plpgsql AS $body$
                BEGIN
                    IF ROW(OLD."RunId",OLD."StageId",OLD."PlanId",OLD."Number",OLD."Round")
                       IS DISTINCT FROM ROW(NEW."RunId",NEW."StageId",NEW."PlanId",NEW."Number",NEW."Round")
                       OR (OLD."AssignmentId" IS NOT NULL AND OLD."AssignmentId" IS DISTINCT FROM NEW."AssignmentId") THEN
                        RAISE EXCEPTION 'Reassignment requires a new sequence attempt' USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END;
                $body$;
                CREATE TRIGGER sequence_attempt_identity BEFORE UPDATE ON operational_sequence_attempt
                    FOR EACH ROW EXECUTE FUNCTION sgol_sequence_attempt_identity();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $body$ BEGIN
                    IF EXISTS (SELECT 1 FROM operational_sequence_run) THEN
                        RAISE EXCEPTION 'Sequence executions exist. Restore a verified backup instead of discarding their history.';
                    END IF;
                END $body$;
                DROP TRIGGER sequence_version_immutable ON operational_sequence_version;
                DROP TRIGGER sequence_step_immutable ON operational_sequence_step;
                DROP TRIGGER sequence_document_producer ON operational_sequence_document;
                DROP TRIGGER sequence_document_append_only ON operational_sequence_document;
                DROP TRIGGER sequence_event_append_only ON operational_sequence_event;
                DROP TRIGGER sequence_attempt_identity ON operational_sequence_attempt;
                DROP FUNCTION sgol_sequence_version_immutable();
                DROP FUNCTION sgol_sequence_document_producer();
                DROP FUNCTION sgol_sequence_append_only();
                DROP FUNCTION sgol_sequence_attempt_identity();
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_operational_sequence_attempt_operational_sequence_run_RunId~",
                table: "operational_sequence_attempt");

            migrationBuilder.DropForeignKey(
                name: "FK_operational_sequence_stage_operational_sequence_run_RunId_V~",
                table: "operational_sequence_stage");

            migrationBuilder.DropForeignKey(
                name: "FK_operational_sequence_attempt_operational_sequence_stage_Run~",
                table: "operational_sequence_attempt");

            migrationBuilder.DropForeignKey(
                name: "FK_sequence_attempt_input_document",
                table: "operational_sequence_attempt");

            migrationBuilder.DropForeignKey(
                name: "FK_sequence_attempt_output_document",
                table: "operational_sequence_attempt");

            migrationBuilder.DropTable(
                name: "operational_sequence_run");

            migrationBuilder.DropTable(
                name: "operational_sequence_stage");

            migrationBuilder.DropTable(
                name: "operational_sequence_step");

            migrationBuilder.DropTable(
                name: "operational_sequence_event");

            migrationBuilder.DropTable(
                name: "operational_sequence_version");

            migrationBuilder.DropTable(
                name: "operational_sequence");

            migrationBuilder.DropTable(
                name: "operational_sequence_document");

            migrationBuilder.DropTable(
                name: "operational_sequence_attempt");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_operational_assignment_Id_PlanId",
                table: "operational_assignment");
        }
    }
}
