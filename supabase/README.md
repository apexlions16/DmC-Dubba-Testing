# Supabase Canlı Katmanı

Bu klasör `dmc-qa-platform` Supabase projesinde çalışan canlı QA servislerinin kaynak aynasını içerir.

Proje ref: `mhzhmeamsvhmcjztsdys`

Canlı fonksiyonlar:

- `qa-api`: operasyonel QA API'si ve proje purge orkestrasyonu.
- `qa-files`: HF S3 direct upload/download, kanıtlar ve build arşiv taşıması.
- `qa-releases`: stable/beta istemci paketleri ve launcher manifesti.
- `qa-secret-bootstrap`: yalnız GitHub Actions OIDC ile secret bootstrap.

## Güvenlik

Bu klasörde secret bulunmaz.

HF credential değerleri GitHub Actions Secrets -> GitHub OIDC -> `qa-secret-bootstrap` -> Supabase Vault akışıyla taşınır.

Public QA tablolarında RLS açıktır ve doğrudan masaüstü client policy'si verilmez. Service-role erişimi yalnız Edge Function runtime içinde kullanılır.

## Depolama

Binary nesneler `xykeskin/dmc-turkish-dub-qa-archive` HF bucket'ında bulunur.

Masaüstü uygulamaları HF secretı almaz. `qa-files` kısa ömürlü S3 URL üretir; uygulama binary veriyi doğrudan HF'ye gönderir veya HF'den alır.

## Purge

Proje purge sırası:

1. Proje CLOSED olmalıdır.
2. İlk admin exact proje adıyla purge talebi açar.
3. Tek kullanımlık DELETE kodu oluşturulur.
4. Farklı ikinci admin kodu onaylar.
5. Önce `projects/{project_id}/` HF prefix'i silinir.
6. Storage silme başarılıysa `qa_finalize_project_purge` PostgreSQL fonksiyonu çalışır.
7. Project-owned DB kayıtları kaldırılır.
8. Global kullanıcı ve cihaz kayıtları korunur.
9. Minimal purge audit tombstone tutulur.

Canlı davranışın ayrıntıları için `docs/canli-supabase-hf-altyapisi.md` dosyasına bakın.
