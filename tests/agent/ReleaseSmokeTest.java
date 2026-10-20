import java.lang.reflect.*;
import java.nio.file.*;

public final class ReleaseSmokeTest {
    public static void main(String[] args) throws Exception {
        Path report = Paths.get(System.getProperty("anisteam.test.report"));
        try {
            Class<?> bridgeType = Class.forName("me.him188.ani.app.domain.watchtogether.LocalPlaybackBridge");
            Class<?> infoType = Class.forName("me.him188.ani.client.models.AniWatchTogetherWatchingInfo");
            Object bridge = bridgeType.getConstructor().newInstance();
            Constructor<?> info = infoType.getConstructor(int.class, int.class, String.class, String.class, String.class,
                    long.class, long.class, long.class, boolean.class, boolean.class, boolean.class, float.class);
            Method update = bridgeType.getMethod("updateLocalWatching$me_him188_ani_app_data", Object.class, infoType);
            Method clear = bridgeType.getMethod("clearLocalWatching$me_him188_ani_app_data", Object.class);
            Object first = new Object();
            Object second = new Object();
            String title = "葬送的芙莉莲\"\\\n😀";
            update.invoke(bridge, first, info.newInstance(1, 3, title, "3", "魔法", 492000L, System.currentTimeMillis(), 1440000L, false, false, false, 1f));
            Thread.sleep(1400);
            update.invoke(bridge, first, info.newInstance(1, 3, title, "3", "魔法", 493000L, 0L, 1440000L, true, false, false, 1f));
            Thread.sleep(1400);
            update.invoke(bridge, first, info.newInstance(1, 3, title, "3", "魔法", 493000L, 0L, 1440000L, false, true, false, 1f));
            Thread.sleep(1400);
            clear.invoke(bridge, first);
            Thread.sleep(1400);
            update.invoke(bridge, second, info.newInstance(1, 4, title, "4", "下一集", 0L, 0L, 0L, true, false, true, 1f));
            Thread.sleep(1400);
            update.invoke(bridge, second, info.newInstance(1, 4, title, "4", "下一集", 10000L, System.currentTimeMillis(), 1440000L, false, false, false, 2f));
            clear.invoke(bridge, first);
            Thread.sleep(1400);
            clear.invoke(bridge, second);
            Thread.sleep(1400);
            Files.writeString(report, "PASS: official LocalPlaybackBridge + model + bundled JVM\n");
        } catch (Throwable ex) {
            Files.writeString(report, "FAIL: " + ex + "\n");
            throw ex;
        }
    }
}
