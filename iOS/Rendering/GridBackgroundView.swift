import SwiftUI

public struct GridBackgroundView: View {
    public var spacing: CGFloat = 24
    public var lineWidth: CGFloat = 0.5
    public var color: Color = .gray.opacity(0.15)

    // Optional overlays (nil by default to keep older call sites working)
    public var viewport: CGRect? = nil
    public var worldBounds: CGRect? = nil
    public var viewSize: CGSize? = nil

    public init(
        spacing: CGFloat = 24,
        lineWidth: CGFloat = 0.5,
        color: Color = .gray.opacity(0.15),
        viewport: CGRect? = nil,
        worldBounds: CGRect? = nil,
        viewSize: CGSize? = nil
    ) {
        self.spacing = spacing
        self.lineWidth = lineWidth
        self.color = color
        self.viewport = viewport
        self.worldBounds = worldBounds
        self.viewSize = viewSize
    }

    public var body: some View {
        Canvas { context, size in
            let cols = Int(ceil(size.width / spacing))
            let rows = Int(ceil(size.height / spacing))
            var path = Path()

            // vertical lines
            for i in 0...cols {
                let x = CGFloat(i) * spacing
                path.move(to: CGPoint(x: x, y: 0))
                path.addLine(to: CGPoint(x: x, y: size.height))
            }
            // horizontal lines
            for j in 0...rows {
                let y = CGFloat(j) * spacing
                path.move(to: CGPoint(x: 0, y: y))
                path.addLine(to: CGPoint(x: size.width, y: y))
            }

            context.stroke(path, with: .color(color), lineWidth: lineWidth)

            // Optional overlays (draw only if everything provided)
            if let viewport, let worldBounds, let viewSize {
                // world bounds (green)
                let wbRect = rect(world: worldBounds, viewport: viewport, viewSize: viewSize)
                context.stroke(Path(wbRect), with: .color(.green.opacity(0.6)), lineWidth: 2)

                // viewport (blue)
                let vpRect = rect(world: viewport, viewport: viewport, viewSize: viewSize)
                context.stroke(Path(vpRect), with: .color(.blue.opacity(0.6)), lineWidth: 2)
            }
        }
        .background(Color.white)
    }

    private func rect(world: CGRect, viewport: CGRect, viewSize: CGSize) -> CGRect {
        // Map world-space rect into view-space rect assuming a simple scale-to-fit
        // (This keeps it deterministic without needing camera state)
        guard viewport.width > 0, viewport.height > 0 else { return .zero }
        let sx = viewSize.width / viewport.width
        let sy = viewSize.height / viewport.height
        let scale = min(sx, sy)
        let tx = -viewport.minX * scale
        let ty = -viewport.minY * scale
        return CGRect(
            x: world.minX * scale + tx,
            y: world.minY * scale + ty,
            width: world.width * scale,
            height: world.height * scale
        )
    }
}
