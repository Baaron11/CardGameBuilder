import Foundation
import CoreGraphics
import Combine

@MainActor
final class ZoomModel: ObservableObject {
    @Published var scale: CGFloat = 1.0
    let minScale: CGFloat = 0.5
    let maxScale: CGFloat = 6.0
    private let step: CGFloat = 0.15

    func clamp(_ s: CGFloat) -> CGFloat { min(max(s, minScale), maxScale) }

    func zoomIn()   { scale = clamp(scale + step) }
    func zoomOut()  { scale = clamp(scale - step) }

    func reset(to target: CGFloat = 1.0) {
        scale = clamp(target)
    }
}
