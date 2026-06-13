# نشر الـ Backend على Railway

الخادم جاهز للنشر على Railway عبر **Dockerfile** (يبني مشروع الـ API فقط — لا تطبيقات MAUI).
ما أُعِدّ مسبقاً: `Dockerfile`, `.dockerignore`, `railway.json`, وقراءة متغيّرات Railway في `Program.cs`.

## ما يقرأه التطبيق تلقائياً من بيئة Railway
| المتغيّر | المصدر | الاستخدام |
|---|---|---|
| `PORT` | يحقنه Railway | يربط الخادم على `0.0.0.0:$PORT` |
| `DATABASE_URL` | إضافة PostgreSQL في Railway | يُحوَّل تلقائياً لسلسلة اتصال Npgsql (مع SSL) |

عند الإقلاع: تُطبَّق هجرات EF تلقائياً وتُبذر بيانات تجريبية إن كانت القاعدة فارغة (مع إعادة محاولة لإقلاع القاعدة).

نقاط مفيدة بعد النشر:
- `/health` → فحص الصحّة (يستخدمه Railway).
- `/swagger` → استكشاف الـ API (الجذر `/` يوجّه إليه).
- `/privacy.html` · `/terms.html` · `/delete-account.html` → الصفحات القانونية.

---

## الطريقة 1: عبر GitHub (موصى بها)
1. ارفع المشروع إلى مستودع GitHub خاص بك:
   ```bash
   git remote add origin https://github.com/<user>/sallimni.git
   git push -u origin main
   ```
2. في Railway: **New Project → Deploy from GitHub repo** واختر المستودع.
3. أضِف قاعدة البيانات: **New → Database → Add PostgreSQL** (سيظهر `DATABASE_URL` تلقائياً في متغيّرات الخدمة، أو اربطه: في خدمة الـ API → Variables → Reference → `DATABASE_URL` من خدمة Postgres).
4. Railway سيكتشف `Dockerfile` ويبني وينشر. تابع السجلّات حتى "Now listening".
5. من **Settings → Networking → Generate Domain** لإصدار رابط عام.

## الطريقة 2: عبر Railway CLI
```bash
npm i -g @railway/cli        # أو: winget install Railway.Railway
railway login                # يفتح المتصفّح لتسجيل الدخول
railway init                 # أنشئ مشروعاً جديداً
railway add --database postgres   # أضِف PostgreSQL
railway up                   # يبني وينشر من المجلّد الحالي (يعتمد Dockerfile)
railway domain               # أصدر رابطاً عاماً
```

---

## ربط التطبيقات بالخادم المنشور
بعد الحصول على الرابط العام (مثل `https://sallimni-api.up.railway.app`)، حدّث عنوان الخادم في كل تطبيق MAUI:
`apps/<App>/MauiProgram.cs` → غيّر `baseUrl` إلى رابط Railway (وأبقِ `https://`).

## ملاحظات
- لا تُثبَّت أسرار في الكود؛ `DATABASE_URL` يأتي من بيئة Railway فقط.
- سلسلة الاتصال المحلّية في `appsettings.json` تُستخدم فقط عند غياب `DATABASE_URL` (للتطوير).
- البذر التجريبي اختياري للإنتاج؛ لإيقافه احذف استدعاء `DataSeeder.SeedAsync` أو اربطه بعلَم بيئة.
