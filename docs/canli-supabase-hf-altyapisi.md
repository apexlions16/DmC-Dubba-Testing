# Canlı Supabase + Hugging Face QA Altyapısı

Bu belge Oyun QA Platformu'nun canlı ortamda veriyi nereye yazdığını, büyük dosyaları nasıl taşıdığını ve bir proje kalıcı olarak silindiğinde hangi adımların uygulandığını açıklar.

## 1. Canlı servisler

### Supabase

Proje: `dmc-qa-platform`

Bölge: `eu-central-1`

Supabase operasyonel veriyi tutar:

- kullanıcılar,
- cihaz eşleşmeleri,
- projeler,
- proje üyelikleri,
- bölüm / Mission kayıtları,
- build metadata,
- görevler,
- görev atamaları,
- bug raporları,
- bug event geçmişi,
- kanıt dosyası metadata,
- yeniden test talepleri,
- yeniden test sonuçları,
- bildirimler ve okundu kayıtları,
- istemci sürüm kayıtları,
- proje purge talepleri,
- minimal purge audit kayıtları.

### Hugging Face Storage Bucket

Bucket:

`xykeskin/dmc-turkish-dub-qa-archive`

Hugging Face yalnız büyük / binary nesneleri tutar:

- oyun veya mod test build'leri,
- bug videoları,
- retest videoları,
- istemci update ZIP paketleri.

Mevcut Hugging Face Dataset kullanılmaz ve bu uygulama tarafından değiştirilmez.

## 2. Canlı Edge API'ler

### qa-api

`https://mhzhmeamsvhmcjztsdys.supabase.co/functions/v1/qa-api/`

Görevleri:

- cihaz enrollment,
- kullanıcı yetkilendirme,
- proje / üyelik yönetimi,
- bölüm yönetimi,
- görevler,
- bug raporları,
- event history,
- retest,
- analytics,
- build metadata,
- bildirimler,
- proje close / purge.

### qa-files

`https://mhzhmeamsvhmcjztsdys.supabase.co/functions/v1/qa-files/`

Görevleri:

- build upload oturumu üretme,
- bug kanıt upload oturumu üretme,
- retest kanıt upload oturumu üretme,
- kısa ömürlü HF S3 URL'leri üretme,
- upload sonrası HF objesini doğrulama,
- build / evidence download yönlendirmesi,
- build'i active namespace'ten archived namespace'e server-side taşıma.

### qa-releases

`https://mhzhmeamsvhmcjztsdys.supabase.co/functions/v1/qa-releases/`

Görevleri:

- stable / beta istemci sürüm kayıtları,
- istemci ZIP upload oturumu,
- launcher latest manifest,
- launcher artifact download yönlendirmesi.

### qa-secret-bootstrap

Normal kullanıcı istemcileri tarafından kullanılmaz.

Yalnız GitHub Actions OIDC kimliğini doğrulayarak GitHub Actions Secrets içindeki HF credential değerlerini Supabase Vault'a aktarır.

## 3. Secret güvenliği

Aşağıdaki değerler EXE içinde bulunmaz:

- `HF_QA_TOKEN`,
- `HF_S3_ACCESS_KEY`,
- `HF_S3_SECRET_KEY`,
- Supabase service role key,
- Vault içeriği.

HF S3 secretları GitHub Actions'ta saklanır. GitHub Actions kısa ömürlü OIDC kimliğiyle `qa-secret-bootstrap` fonksiyonuna bağlanır ve değerleri Supabase Vault'a aktarır.

Masaüstü istemcisi yalnız kendi Device credential değerini DPAPI ile Windows kullanıcı profiline bağlı biçimde saklar.

## 4. Doğrudan dosya yükleme

Büyük dosyalar Supabase Edge Function üzerinden proxy edilmez.

Akış:

1. Masaüstü uygulaması dosyanın SHA-256 özetini yerelde hesaplar.
2. `qa-files` API'sinden kısa ömürlü bir upload URL ister.
3. API kullanıcının proje / rol yetkisini doğrular.
4. API yalnız ilgili immutable `project_id` namespace'i için URL üretir.
5. Masaüstü uygulaması dosyayı doğrudan Hugging Face S3 gateway'e gönderir.
6. Upload tamamlanınca istemci finalize endpoint'ini çağırır.
7. `qa-files`, HF üzerinde HEAD kontrolü ile objenin mevcut olduğunu ve boyutunu doğrular.
8. Yalnız doğrulama başarılıysa Supabase metadata kaydı tamamlanır.

Böylece HF write secretı tester veya developer bilgisayarına verilmez.

## 5. HF klasör / prefix düzeni

Her proje immutable UUID ile ayrılır.

Örnek:

```text
projects/
  {project_id}/
    builds/
      active/
        {build_id}/
          build.zip
      archived/
        {build_id}/
          build.zip
    reports/
      {bug_public_key}/
        evidence/
          {asset_id}/
            video.mp4
    retests/
      {retest_request_id}/
        evidence/
          {asset_id}/
            retest.mp4
```

Bir projenin adı değişse bile storage sınırı değişmez; silme ve erişim kararları isimle değil immutable `project_id` ile yapılır.

## 6. Build upload ve yayınlama

Developer veya Admin:

1. build metadata oluşturur,
2. dosyayı seçer,
3. SHA-256 hesaplanır,
4. build doğrudan HF `builds/active/{build_id}` yoluna yüklenir,
5. HF objesi doğrulanır,
6. build `candidate` durumuna geçer.

Candidate build testerların current build'i değildir.

Yalnız Admin `Publish` işlemiyle build'i `current` yapar.

## 7. Build arşivleme

Arşivleme yalnız Admin tarafından yapılabilir.

Engeller:

- Current build doğrudan arşivlenemez.
- Açık bir görev build'i kullanıyorsa arşivlenemez.
- Dosyası olmayan build arşivlenemez.

Başarılı arşiv akışı:

1. HF `CopyObject` ile `builds/active/...` objesi `builds/archived/...` yoluna server-side kopyalanır.
2. Hedef objenin varlığı doğrulanır.
3. Kaynak active objesi silinir.
4. Supabase `storage_path`, `status=archived` ve `archived_at` alanlarını günceller.
5. Build event oluşturulur.

Dosya kullanıcı bilgisayarından tekrar upload edilmez.

## 8. Bug ve retest kanıtları

Tester video seçtiğinde:

- bug report önce DB'de oluşturulur,
- video upload başarısız olursa ikinci bir bug oluşturulmaz,
- yalnız video yeniden denenebilir,
- video başarılı yüklenince evidence metadata Supabase'e yazılır.

Retest evidence aynı prensiple ayrı retest namespace'ine gider.

## 9. Proje kapatma

`ACTIVE -> CLOSED`

Close işlemi veri silmez.

Closed proje:

- yeni normal test akışına kapatılabilir,
- geçmiş raporları ve istatistikleri korunur,
- gerektiğinde kalıcı purge için aday olur.

## 10. Kalıcı proje silme

Kalıcı silme ile Close aynı işlem değildir.

Purge güvenlik zinciri:

1. Proje önce `CLOSED` olmalıdır.
2. Admin silme önizlemesini görür.
3. Önizlemede bug / video / retest / görev / build / üye / bölüm sayıları gösterilir.
4. Admin exact proje adını yazar.
5. Sistem tek kullanımlık `DELETE-XXXXXX` kodu üretir.
6. Purge talebi `PURGE_PENDING` olur.
7. Talebi açan Admin kendi talebini onaylayamaz.
8. İkinci Admin exact proje adı + confirmation code ile onay verir.
9. Önce HF `projects/{project_id}/**` namespace'i silinir.
10. HF silme başarısızsa DB silinmez.
11. HF başarılıysa PostgreSQL purge transaction çalışır.
12. Project-owned DB verileri silinir.
13. Minimal purge audit tombstone yazılır.

## 11. DB purge sırasında silinen proje verileri

Purge ile proje sahipliğindeki kayıtlar temizlenir:

- project member ilişkileri,
- sections,
- builds,
- build fix candidates,
- build events,
- tasks,
- task assignees,
- bug reports,
- bug events,
- evidence metadata,
- retest requests,
- retest assignees,
- retest results,
- proje bildirimleri,
- bildirim recipient kayıtları,
- proje counter verileri,
- purge request içeriği.

Retest tabloları bug ve build tablolarına birden fazla FK ile bağlı olduğundan purge fonksiyonu retest zincirini deterministik sırada kaldırır ve ardından project cascade çalışır.

## 12. Global kullanıcılar neden silinmez?

`users` ve `devices` global platform varlıklarıdır.

Örnek:

Ahmet hem DmC hem Project B üzerinde tester olabilir.

DmC purge edildiğinde Ahmet hesabını silmek Project B erişimini de yok eder. Bu nedenle proje purge yalnız `project_members` ilişkisini ve o projeye ait operasyonel kayıtları siler.

Kullanıcının platformdan tamamen silinmesi ileride ayrı bir Admin kullanıcı silme operasyonu olarak ele alınmalıdır.

## 13. Purge audit tombstone

İçerik verisi tutulmaz.

Minimal audit alanları:

- project id,
- project key,
- proje adının SHA-256 hash'i,
- oluşturulma / kapanma / purge zamanı,
- purge isteyen admin,
- onaylayan ikinci admin,
- silinen rapor sayısı,
- silinen build sayısı,
- silinen evidence sayısı,
- silinen retest sayısı,
- silinen bildirim sayısı,
- silinen storage byte miktarı.

Bug açıklaması, video, tester yorumu veya proje içeriği audit tombstone içinde tutulmaz.

## 14. Doğrulanmış canlı smoke testler

Gerçek disposable projeler kullanılarak aşağıdaki testler yapıldı:

- Supabase Device authentication,
- proje oluşturma,
- proje üyeliği,
- section oluşturma,
- ortak task oluşturma,
- bug raporu,
- bug event history,
- analytics,
- DB-only iki-admin purge,
- global kullanıcının purge sonrasında korunması,
- HF S3 PutObject,
- HF S3 GetObject,
- HF S3 ListObjectsV2,
- HF S3 DeleteObject,
- presigned build PUT / GET,
- presigned bug evidence PUT / GET,
- presigned retest evidence PUT,
- evidence metadata,
- HF project-prefix purge,
- Supabase cascade purge,
- build active -> archived server-side move,
- archived build purge,
- istemci release ZIP upload,
- latest release manifest,
- launcher artifact download ve SHA doğrulaması.

Disposable CI proje, release ve HF objeleri test sonunda temizlenir.

## 15. RLS yaklaşımı

QA tablolarında Row Level Security aktiftir ve doğrudan `anon` / `authenticated` client policy verilmez.

Bu kasıtlıdır.

Windows istemcileri Supabase Data API'yi doğrudan kullanmaz. Yetkilendirme `qa-api`, `qa-files` ve `qa-releases` Edge Function katmanlarında Device credential + proje üyeliği + rol kontrolüyle yapılır.

## 16. Canlı Windows istemci yapılandırması

Tester ve Yönetim Merkezi paketlerinde:

```text
qa-api-endpoint.txt
```

şu API'yi gösterir:

```text
https://mhzhmeamsvhmcjztsdys.supabase.co/functions/v1/qa-api/
```

Launcher ayrı olarak `qa-releases` endpoint'ini kullanır.

Secret veya token bu config dosyalarında bulunmaz.
