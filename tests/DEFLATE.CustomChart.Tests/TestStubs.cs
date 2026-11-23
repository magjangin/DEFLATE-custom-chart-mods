namespace DEFLATE_custom_chart.Core
{
    /// <summary>테스트용 ModLog 스텁 (실제 ModLog는 MelonLoader에 의존). 링크된 파서 코드가 부르는 메서드만 둔다.</summary>
    public static class ModLog
    {
        public static void Verbose(string message) { }
        public static void Debug(string message) { }
        public static void Info(string message) { }
    }
}
