import SwiftUI
import PencilKit

struct PencilCanvasRepresentable: UIViewRepresentable {
    @ObservedObject var pk: PencilKitManager
    @ObservedObject var zoom: ZoomModel

    func makeUIView(context: Context) -> PKCanvasView {
        let v = PKCanvasView()
        v.backgroundColor = .clear
        pk.attach(to: v)
        v.zoomScale = zoom.scale
        return v
    }

    func updateUIView(_ uiView: PKCanvasView, context: Context) {
        // Keep zoom in sync (tolerance avoids feedback loop)
        let tol: CGFloat = 0.01
        if abs(uiView.zoomScale - zoom.scale) > tol {
            uiView.zoomScale = zoom.scale
        }
    }
}

struct WhiteboardView: View {
    @ObservedObject var pkManager: PencilKitManager
    @ObservedObject var zoomModel: ZoomModel

    var body: some View {
        ZStack {
            GridBackgroundView()
                .ignoresSafeArea(.all, edges: .all)
            PencilCanvasRepresentable(pk: pkManager, zoom: zoomModel)
        }
    }
}
