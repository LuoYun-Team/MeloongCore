namespace MeloongCore.Tests;

public class ProgressTest : TestBase {

    [Test]
    public async Task ProgressChanged_主进度实际改变时触发() {
        var progress = new ProgressProvider();
        int changedCount = 0;
        progress.ProgressChanged += () => changedCount++;

        progress.Set(0.2);
        progress.Set(0.2);
        progress.Add(0.1);
        progress.Add(0);
        progress.Set(0.5, skiped: true);

        await Assert.That(changedCount).IsEqualTo(3);
    }

    [Test]
    public async Task ProgressChanged_子进度改变时向父级传播() {
        var progress = new ProgressProvider();
        int changedCount = 0;
        progress.ProgressChanged += () => changedCount++;

        var sub = progress.SplitBy(0.5).Single();
        sub.Set(0.5);
        sub.Set(0.5);

        await Assert.That(changedCount).IsEqualTo(2);
    }

    [Test]
    public async Task ProgressChanged_不会触发Observe() {
        var progress = new ProgressProvider();
        int changedCount = 0;
        progress.ProgressChanged += () => changedCount++;

        progress.Set(0.5);
        progress.Set(0.75, skiped: true);

        await Assert.That(changedCount).IsEqualTo(2);
        await Assert.That(Math.Abs(progress.Observe() - 2.0 / 3) < 0.000001).IsTrue();
    }

}
