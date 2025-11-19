import Foundation
import Combine
import PencilKit
import UIKit

@MainActor
final class PencilKitManager: ObservableObject {
    // Basic observable state (expand later)
    @Published var color: UIColor = .systemBlue
    @Published var width: CGFloat = 4.0
    @Published var isEraser: Bool = false

    weak var canvasView: PKCanvasView?

    func attach(to canvas: PKCanvasView) {
        self.canvasView = canvas
        canvas.drawingPolicy = .anyInput
        canvas.minimumZoomScale = 0.5
        canvas.maximumZoomScale = 6.0
        applyTool()
    }

    func setInk(_ type: PKInkingTool.InkType) {
        isEraser = false
        applyTool(type: type)
    }

    func setEraser(_ type: PKEraserTool.EraserType = .vector) {
        isEraser = true
        canvasView?.tool = PKEraserTool(type: type)
    }

    func setColor(_ ui: UIColor) {
        color = ui
        if !isEraser { applyTool() }
    }

    func setWidth(_ w: CGFloat) {
        width = w
        if !isEraser { applyTool() }
    }

    private func applyTool(type: PKInkingTool.InkType = .pen) {
        canvasView?.tool = PKInkingTool(type, color: color, width: width)
    }

    func clear() { canvasView?.drawing = PKDrawing() }
    func undo()  { canvasView?.undoManager?.undo() }
    func redo()  { canvasView?.undoManager?.redo() }
}
