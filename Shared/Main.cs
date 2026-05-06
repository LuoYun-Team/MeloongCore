namespace MeloongCore;
public static class Main {

    public static void Initialize(BaseLogger? logger = null) {
        if (logger is not null) Logger.Instance = logger;
    }

}
