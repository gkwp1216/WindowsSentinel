namespace LogCheck.Services
{
    /// <summary>
    /// 페이지 네비게이션 인터페이스
    /// BasePageViewModel 패턴에서 사용되는 네비게이션 관리 인터페이스
    /// </summary>
    public interface INavigable
    {
        /// <summary>
        /// 페이지에서 나갈 때 호출
        /// </summary>
        void OnNavigatedFrom();

        /// <summary>
        /// 페이지로 진입할 때 호출
        /// </summary>
        void OnNavigatedTo();
    }
}