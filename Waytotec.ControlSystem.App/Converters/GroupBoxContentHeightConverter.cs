using System.Globalization;
using System.Windows.Data;

namespace Waytotec.ControlSystem.App.Converters
{
    /// <summary>
    /// GroupBox의 실제 높이에서 헤더와 패딩을 제외한 콘텐츠 영역의 높이를 계산하는 컨버터
    /// </summary>
    public class GroupBoxContentHeightConverter : IValueConverter
    {
        // GroupBox 헤더와 패딩을 고려한 오프셋 값
        // 이 값은 사용하는 GroupBox 스타일에 따라 조정이 필요할 수 있습니다
        private const double HeaderAndPaddingOffset = 50;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double actualHeight && actualHeight > 0)
            {
                // GroupBox의 실제 높이에서 헤더와 패딩을 뺀 값을 반환
                double contentHeight = actualHeight - HeaderAndPaddingOffset;

                // 최소 높이를 보장 (음수가 되지 않도록)
                return Math.Max(contentHeight, 100);
            }

            // 기본값 반환
            return 300;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}