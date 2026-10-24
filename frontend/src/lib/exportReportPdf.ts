import type { UsageReportDto } from '../api/types'
import {
  generatedText,
  periodText,
  isNumericColumn,
  REPORT_COLUMNS,
  reportFileName,
  roomRowText,
  thresholdsText,
} from './reportFormat'

/** Light amber fill for flagged rows. jsPDF cannot read the MUI theme, so this is the one literal colour. */
const FLAGGED_ROW_FILL: [number, number, number] = [255, 243, 205]

/**
 * Builds the PDF from the report that is already on screen and saves it. The libraries are loaded here, on demand,
 * so the PDF code stays out of the main bundle. No number is calculated: rows, order and flags come from the API.
 */
export async function exportReportPdf(report: UsageReportDto): Promise<void> {
  const [{ jsPDF }, { autoTable }] = await Promise.all([import('jspdf'), import('jspdf-autotable')])

  const doc = new jsPDF({ orientation: 'landscape', unit: 'mm', format: 'a4' })
  const margin = 14

  doc.setFontSize(18)
  doc.text('Room usage report', margin, 18)
  doc.setFontSize(10)
  doc.text(generatedText(report), margin, 25)
  doc.text(`Period: ${periodText(report)}`, margin, 30)
  const pageContentWidth = doc.internal.pageSize.getWidth() - 2 * margin
  doc.text(doc.splitTextToSize(thresholdsText(report), pageContentWidth), margin, 35)

  const numericColumns: Record<number, { halign: 'right' }> = {}
  REPORT_COLUMNS.forEach((_, column) => {
    if (isNumericColumn(column)) {
      numericColumns[column] = { halign: 'right' }
    }
  })

  autoTable(doc, {
    startY: 40,
    margin: { left: margin, right: margin, bottom: 16 },
    head: [[...REPORT_COLUMNS]],
    body: report.rooms.map(roomRowText),
    styles: { fontSize: 9 },
    headStyles: { fillColor: [60, 60, 60] },
    columnStyles: numericColumns,
    showHead: 'everyPage',
    didParseCell: (data) => {
      if (data.section === 'body' && report.rooms[data.row.index]?.isOftenBookedRarelyUsed === true) {
        data.cell.styles.fillColor = FLAGGED_ROW_FILL
      }
    },
  })

  const pageCount = doc.getNumberOfPages()
  const pageWidth = doc.internal.pageSize.getWidth()
  const pageHeight = doc.internal.pageSize.getHeight()
  doc.setFontSize(9)
  for (let page = 1; page <= pageCount; page += 1) {
    doc.setPage(page)
    doc.text(`Page ${page} of ${pageCount}`, pageWidth - margin, pageHeight - 8, { align: 'right' })
  }

  doc.save(reportFileName(report))
}
