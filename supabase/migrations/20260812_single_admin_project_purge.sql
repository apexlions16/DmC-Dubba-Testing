-- Tek yönetici kalıcı proje silme akışı.
-- UI exact proje adı doğrulaması yapar; qa-files önce HF namespace'ini temizler,
-- ardından bu RPC aynı yönetici kimliğiyle project-owned DB verisini kaldırır.

create or replace function public.qa_finalize_project_purge(
  p_project_id text,
  p_requested_by text,
  p_approved_by text,
  p_storage_deleted_bytes bigint default 0
)
returns void
language plpgsql
set search_path to 'public','extensions'
as $function$
declare
  v_project public.projects%rowtype;
  v_reports bigint;
  v_builds bigint;
  v_evidence bigint;
  v_retests bigint;
  v_notifications bigint;
begin
  select * into v_project
    from public.projects
   where id=p_project_id
   for update;

  if not found then
    raise exception 'PROJECT_NOT_FOUND';
  end if;

  if v_project.status <> 'closed' and v_project.status <> 'purge_pending' then
    raise exception 'PROJECT_MUST_BE_CLOSED';
  end if;

  select count(*) into v_reports from public.bug_reports where project_id=p_project_id;
  select count(*) into v_builds from public.builds where project_id=p_project_id;
  select count(*) into v_evidence from public.evidence_assets where project_id=p_project_id;
  select count(*) into v_retests
    from public.retest_requests r
    join public.bug_reports b on b.id=r.bug_report_id
   where b.project_id=p_project_id;
  select count(*) into v_notifications from public.notifications where project_id=p_project_id;

  insert into public.project_purge_audits(
    project_id, project_key, project_name_hash, created_at, closed_at, purged_at,
    requested_by, approved_by, reports_deleted_count, builds_deleted_count,
    evidence_deleted_count, retests_deleted_count, notifications_deleted_count,
    storage_deleted_bytes
  ) values (
    v_project.id,
    v_project.key,
    encode(extensions.digest(v_project.name::text,'sha256'::text),'hex'),
    v_project.created_at,
    v_project.closed_at,
    now(),
    p_requested_by,
    p_approved_by,
    v_reports,
    v_builds,
    v_evidence,
    v_retests,
    v_notifications,
    coalesce(p_storage_deleted_bytes,0)
  );

  delete from public.retest_results rr
   where rr.retest_request_id in (
     select r.id
       from public.retest_requests r
       join public.bug_reports b on b.id=r.bug_report_id
      where b.project_id=p_project_id
   );

  delete from public.retest_assignees ra
   where ra.retest_request_id in (
     select r.id
       from public.retest_requests r
       join public.bug_reports b on b.id=r.bug_report_id
      where b.project_id=p_project_id
   );

  delete from public.retest_requests r
   where r.bug_report_id in (
     select b.id from public.bug_reports b where b.project_id=p_project_id
   );

  delete from public.projects where id=p_project_id;
end;
$function$;
