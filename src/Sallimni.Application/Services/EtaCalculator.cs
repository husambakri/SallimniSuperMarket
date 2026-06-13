namespace Sallimni.Application.Services;

/// <summary>تفصيل تقدير وقت التسليم عند الطلب.</summary>
public record EtaEstimate(
    DateTimeOffset CollectionWaveAt,
    DateTimeOffset DistributionWaveAt,
    DateTimeOffset Eta,
    bool RolledToNextWave);

/// <summary>
/// التقدير عند الطلب (Wave-based) — قسم 6.1.
/// شرط اللحاق بموجة الجمع: T_now + max(T_prep) &lt; T_coll.
/// يُلحَق ⇒ ETA = T_dist + T_transit، وإلا يُرحَّل للموجة التالية.
/// </summary>
public static class EtaCalculator
{
    public static EtaEstimate Estimate(
        DateTimeOffset now,
        int maxPrepMinutes,
        int transitMinutes,
        int waveIntervalMinutes,
        int distributionGapMinutes)
    {
        var collectionWave = WaveScheduler.NextBoundaryAtOrAfter(now, waveIntervalMinutes);
        var rolled = false;

        // لا يلحق بموجة الجمع القادمة إن لم يكتمل التحضير قبلها.
        if (now.AddMinutes(maxPrepMinutes) >= collectionWave)
        {
            collectionWave = collectionWave.AddMinutes(waveIntervalMinutes);
            rolled = true;
        }

        var distributionWave = collectionWave.AddMinutes(distributionGapMinutes);
        var eta = distributionWave.AddMinutes(transitMinutes);

        return new EtaEstimate(collectionWave, distributionWave, eta, rolled);
    }
}
