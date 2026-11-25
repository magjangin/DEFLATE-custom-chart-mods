namespace DEFLATE_custom_chart.Core.Wrapper
{
    /// <summary>
    /// Harmony <c>object[] __args</c>에서 인자를 <b>타입</b>으로 찾는 헬퍼 (파라미터 이름 비의존 바인딩).
    ///
    /// <b>왜 필요한가:</b> Harmony는 Prefix/Postfix 파라미터를 원본 파라미터 <b>이름</b>으로 연결합니다
    /// (<c>string trackID, LaneController lane</c>). 게임 업데이트로 이름만 바뀌어도(<c>trackID</c> → <c>trackId</c>)
    /// 패치 단계에서 예외가 나 그 훅 전체가 빠집니다. BMS 노트·라인바 주입처럼 모드의 핵심 훅이 이렇게 빠지면
    /// 조용히 원곡 차트로 플레이됩니다. <c>__args</c>를 받아 타입으로 고르면 이름 변경과 인자 순서 변경 모두에 견딥니다.
    ///
    /// <b>한계:</b> 같은 타입의 인자가 둘 이상이면 첫 번째를 고릅니다(대상 시그니처를 확인하고 쓸 것).
    /// 호출마다 <c>object[]</c> 할당·박싱이 생기므로 매 프레임 불리는 핫패스에는 쓰지 마세요(레인 로드처럼 곡당 수 회인 훅용).
    /// <c>ref</c> 인자를 <b>바꿔야</b> 하는 훅에는 쓰지 말고 이름 바인딩을 유지합니다(<c>__args</c> 쓰기 반영은 Harmony 버전에 따라 다름).
    ///
    /// <b>적용 원칙 (v1.1.5 정리):</b> 훅이 원본 파라미터를 <b>쓰지 않으면 선언하지 않습니다</b>(선언만 해도 이름이 바뀌면 패치 실패).
    /// 읽기만 하고 곡·화면당 몇 번 불리는 훅은 <c>__args</c>, 매 프레임 훅·<c>ref</c>를 바꾸는 훅·같은 타입 인자가 둘 이상인 훅만 이름 바인딩을 남깁니다.
    /// 남은 이름 바인딩은 <c>tools/check_hook_targets.py</c>가 새 덤프와 대조합니다(AGENTS.md §3-⑤ ⑧).
    /// </summary>
    public static class HookArgs
    {
        /// <summary>인자 중 <typeparamref name="T"/> 타입인 첫 번째 값. 없으면 null.</summary>
        public static T First<T>(object[] args) where T : class
        {
            if (args == null) return null;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] is T value) return value;
            }
            return null;
        }

        /// <summary>문자열 인자 전부를 <c>'a', 'b'</c> 꼴로 (로그용 — ModLog 보간 구멍 안에서만 부를 것).</summary>
        public static string DescribeStrings(object[] args)
        {
            if (args == null) return "(없음)";
            var parts = new System.Collections.Generic.List<string>();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] is string s) parts.Add("'" + s + "'");
            }
            return parts.Count > 0 ? string.Join(", ", parts) : "(없음)";
        }

        /// <summary>인자 중 첫 번째 <c>int</c>. 없으면 <paramref name="fallback"/>.</summary>
        public static int FirstInt(object[] args, int fallback) => FirstValue(args, fallback);

        /// <summary>인자 중 <typeparamref name="T"/> 값 타입(bool·float 등)인 첫 번째 값. 없으면 <paramref name="fallback"/>.</summary>
        public static T FirstValue<T>(object[] args, T fallback) where T : struct
        {
            if (args == null) return fallback;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] is T value) return value;
            }
            return fallback;
        }
    }
}
