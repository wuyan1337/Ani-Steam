package dev.anisteam;

import java.io.*;
import java.lang.instrument.*;
import java.lang.ref.WeakReference;
import java.lang.reflect.Method;
import java.nio.charset.StandardCharsets;
import java.nio.file.*;
import java.security.ProtectionDomain;
import java.time.Instant;
import java.util.concurrent.*;
import org.objectweb.asm.*;

public final class Agent {
    private static final String TARGET = "me/him188/ani/app/domain/watchtogether/LocalPlaybackBridge";
    private static volatile WeakReference<Object> bridge = new WeakReference<>(null);
    private static volatile boolean hookReady;
    private static volatile String error;
    private static Process helper;
    private static BufferedWriter output;
    private static long nextStart;
    private static Path directory;
    private static Path logPath;
    private static Method flowValue;
    private static final long PID = ProcessHandle.current().pid();

    public static void premain(String options, Instrumentation instrumentation) {
        try {
            directory = Paths.get(Agent.class.getProtectionDomain().getCodeSource().getLocation().toURI()).getParent();
            String local = System.getenv("LOCALAPPDATA");
            logPath = Paths.get(local == null ? System.getProperty("java.io.tmpdir") : local, "AniSteam", "agent.log");
            String version = System.getProperty("jpackage.app-version", "unknown");
            if (version.equals("6.3.0")) instrumentation.addTransformer(new PlaybackTransformer());
            else error = "Unsupported Animeko version: " + version + " (expected 6.3.0)";
            ScheduledExecutorService executor = Executors.newSingleThreadScheduledExecutor(r -> {
                Thread t = new Thread(r, "AniSteam-Observer"); t.setDaemon(true); return t;
            });
            executor.scheduleWithFixedDelay(Agent::tick, 0, 250, TimeUnit.MILLISECONDS);
            log("Observer started for Animeko 6.3.0 (pid " + PID + ")");
        } catch (Throwable failure) {
            log("Agent initialization failed: " + failure.getClass().getSimpleName());
        }
    }

    public static void capture(Object value) {
        try { bridge = new WeakReference<>(value); }
        catch (Throwable ignored) { }
    }

    static final class PlaybackTransformer implements ClassFileTransformer {
        @Override public byte[] transform(ClassLoader loader, String name, Class<?> redefining,
                ProtectionDomain domain, byte[] bytes) {
            if (!TARGET.equals(name)) return null;
            try {
                ClassReader reader = new ClassReader(bytes);
                ClassWriter writer = new ClassWriter(reader, ClassWriter.COMPUTE_MAXS);
                boolean[] found = { false };
                reader.accept(new ClassVisitor(Opcodes.ASM9, writer) {
                    @Override public MethodVisitor visitMethod(int access, String method, String desc, String signature, String[] exceptions) {
                        MethodVisitor delegate = super.visitMethod(access, method, desc, signature, exceptions);
                        if (!method.equals("<init>") || !desc.equals("()V")) return delegate;
                        found[0] = true;
                        return new MethodVisitor(Opcodes.ASM9, delegate) {
                            @Override public void visitInsn(int opcode) {
                                if (opcode == Opcodes.RETURN) {
                                    super.visitVarInsn(Opcodes.ALOAD, 0);
                                    super.visitMethodInsn(Opcodes.INVOKESTATIC, "dev/anisteam/Agent", "capture", "(Ljava/lang/Object;)V", false);
                                }
                                super.visitInsn(opcode);
                            }
                        };
                    }
                }, 0);
                if (!found[0]) throw new IllegalStateException("Playback bridge constructor is unavailable");
                hookReady = true;
                log("Playback bridge hook installed");
                return writer.toByteArray();
            } catch (Throwable failure) {
                error = "Unsupported playback bridge: " + failure.getClass().getSimpleName();
                log(error);
                return null;
            }
        }
    }

    private static void tick() {
        try {
            String watching = "null";
            Object current = bridge.get();
            if (current != null) {
                Object flow = current.getClass().getMethod("getLocalWatching").invoke(current);
                if (flowValue == null) flowValue = Class.forName("kotlinx.coroutines.flow.StateFlow", false,
                        current.getClass().getClassLoader()).getMethod("getValue");
                Object value = flowValue.invoke(flow);
                if (value != null) watching = encodeWatching(value);
                error = null;
            }
            String json = "{\"schema\":1,\"sourcePid\":" + PID + ",\"hookReady\":" + hookReady
                    + ",\"error\":" + quote(error) + ",\"watching\":" + watching + "}";
            write(json);
        } catch (Throwable failure) {
            String diagnostic = "Playback observer: " + failure.getClass().getSimpleName();
            if (!diagnostic.equals(error)) log(diagnostic);
            error = diagnostic;
            try { write("{\"schema\":1,\"sourcePid\":" + PID + ",\"hookReady\":" + hookReady
                    + ",\"error\":" + quote(error) + ",\"watching\":null}"); }
            catch (Throwable ignored) { }
        }
    }

    private static String encodeWatching(Object value) throws ReflectiveOperationException {
        StringBuilder result = new StringBuilder("{");
        String[] names = { "SubjectId", "EpisodeId", "SubjectName", "EpisodeSort", "EpisodeName",
                "PositionMillis", "DurationMillis", "Paused", "Buffering", "Loading", "PlaybackRate", "PositionAtMillis" };
        for (String name : names) {
            if (result.length() > 1) result.append(',');
            Object field = value.getClass().getMethod("get" + name).invoke(value);
            result.append(quote(Character.toLowerCase(name.charAt(0)) + name.substring(1))).append(':');
            result.append(field instanceof String ? quote((String)field) : String.valueOf(field));
        }
        return result.append('}').toString();
    }

    private static void write(String line) throws IOException {
        String testOutput = System.getProperty("anisteam.test.output");
        if (testOutput != null) {
            Files.writeString(Paths.get(testOutput), line + "\n", StandardCharsets.UTF_8, StandardOpenOption.CREATE, StandardOpenOption.APPEND);
            return;
        }
        if (helper == null || !helper.isAlive()) {
            if (System.nanoTime() < nextStart) return;
            nextStart = System.nanoTime() + TimeUnit.SECONDS.toNanos(15);
            if (output != null) { try { output.close(); } catch (IOException ignored) { } output = null; }
            Path exe = directory.resolve("AniSteam.exe");
            if (!Files.isRegularFile(exe)) throw new FileNotFoundException("AniSteam.exe");
            helper = new ProcessBuilder(exe.toString(), "--parent-pid", Long.toString(PID))
                    .directory(directory.toFile()).redirectOutput(ProcessBuilder.Redirect.DISCARD)
                    .redirectError(ProcessBuilder.Redirect.DISCARD).start();
            output = new BufferedWriter(new OutputStreamWriter(helper.getOutputStream(), StandardCharsets.UTF_8));
        }
        output.write(line); output.newLine(); output.flush();
    }

    static String quote(String text) {
        if (text == null) return "null";
        StringBuilder result = new StringBuilder("\"");
        for (int i = 0; i < text.length(); i++) {
            char c = text.charAt(i);
            if (c == '\\' || c == '"') result.append('\\').append(c);
            else if (c < 0x20 || Character.isSurrogate(c)) result.append(String.format("\\u%04x", (int)c));
            else result.append(c);
        }
        return result.append('"').toString();
    }

    private static void log(String message) {
        try {
            if (logPath == null) return;
            Files.createDirectories(logPath.getParent());
            if (Files.exists(logPath) && Files.size(logPath) > 1024 * 1024)
                Files.move(logPath, logPath.resolveSibling("agent.log.1"), StandardCopyOption.REPLACE_EXISTING);
            Files.writeString(logPath, Instant.now() + " " + message + "\n", StandardCharsets.UTF_8,
                    StandardOpenOption.CREATE, StandardOpenOption.APPEND);
        } catch (Throwable ignored) { }
    }
}
